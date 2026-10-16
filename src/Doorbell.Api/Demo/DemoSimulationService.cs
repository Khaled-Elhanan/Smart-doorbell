using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Recipients;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Abstractions.Storage;
using Doorbell.Application.Features.Devices.Commands.ConfigureDoorbell;
using Doorbell.Application.Features.Devices.Commands.CreateDevice;
using Doorbell.Application.Features.Doorbell.Commands.AcknowledgeRing;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Infrastructure.Messaging.Mqtt.Models;
using Doorbell.Infrastructure.Storage;
using MediatR;
using Microsoft.Extensions.Options;

namespace Doorbell.Api.Demo;

public sealed record DemoCommandStatus(
    string? RequestId,
    string? Status,
    int Attempt,
    string? StorageKey);

public sealed record DemoOutboxStatus(
    string Kind,
    string? RecipientUserId,
    string? RecipientName,
    string Status,
    int AttemptCount,
    string? LastChannel,
    bool LastUsedFallback,
    DateTime? LastAttemptAtUtc,
    string? LastError,
    bool Exhausted);

public sealed record DemoAssetStatus(
    bool SoundPresent,
    bool SampleImagePresent,
    string[] Missing);

public sealed record DemoTimelineEvent(
    string Key,
    string Label,
    string State,
    DateTime? AtUtc,
    long? DurationMs,
    string? Detail);

public sealed record DemoSnapshotMeta(
    string? StorageKey,
    string? Resolution,
    DateTime? CapturedAtUtc,
    long? SizeBytes,
    string? MimeType);

public sealed record DemoStatusResponse(
    string RunId,
    string Mode,
    bool EventExists,
    string? RingEventId,
    string? SnapshotStatus,
    string? ChimeStatus,
    DemoCommandStatus? SnapshotCommand,
    DemoCommandStatus? ChimeCommand,
    DemoOutboxStatus? RingAlert,
    DemoOutboxStatus? SnapshotDelivery,
    DemoOutboxStatus? Notification,
    string? AcknowledgeStatus,
    string? ImageUrl,
    string Phase,
    bool RunCompleted,
    bool AwaitingAck,
    bool Cancelled,
    DateTime? AcknowledgedAtUtc,
    long? AckWaitMs,
    DemoSnapshotMeta SnapshotMeta,
    DemoTimelineEvent[] Timeline,
    DemoAssetStatus Assets);

public sealed record DemoRunSummary(
    string RunId,
    string Mode,
    string Phase,
    bool AwaitingAck,
    bool Cancelled,
    DateTime RequestedAtUtc);

public sealed record DemoAckResult(
    string RunId,
    bool Acknowledged,
    bool AlreadyAcknowledged,
    bool Cancelled,
    DateTime? AcknowledgedAtUtc,
    long? AckWaitMs,
    string? Message);

public sealed class DemoSimulationService
{
    private sealed class DemoFixture
    {
        public Guid DeviceId { get; init; }

        public string CameraEntityCode { get; init; } = null!;

        public string ChimeEntityCode { get; init; } = null!;

        public string MelodyId { get; init; } = null!;

        public string RingTopic => $"devices/{DeviceId}/doorbell/ring";
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMqttCommandPublisher _publisher;
    private readonly IRecipientProvider _recipients;
    private readonly IOptions<DemoOptions> _options;
    private readonly IWebHostEnvironment _environment;
    private readonly DemoRunStore _runs;
    private readonly ILogger<DemoSimulationService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private DemoFixture? _autoFixture;
    private DemoFixture? _interactiveFixture;

    public DemoSimulationService(
        IServiceScopeFactory scopeFactory,
        IMqttCommandPublisher publisher,
        IRecipientProvider recipients,
        IOptions<DemoOptions> options,
        IWebHostEnvironment environment,
        DemoRunStore runs,
        ILogger<DemoSimulationService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _recipients = recipients;
        _options = options;
        _environment = environment;
        _runs = runs;
        _logger = logger;
    }

    public async Task<string> StartRingAsync(
        DemoMode mode,
        CancellationToken cancellationToken)
    {
        var fixture = await EnsureFixtureAsync(mode, cancellationToken);

        var runId = "demo-" + Guid.NewGuid().ToString("N")[..10];

        _runs.Create(runId, fixture.DeviceId, mode);

        await _publisher.PublishAsync(
            fixture.RingTopic,
            new RingMqttMessage(
                fixture.DeviceId,
                runId,
                DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation(
            "Demo: published ring {RunId} for device {DeviceId} (mode {Mode}).",
            runId,
            fixture.DeviceId,
            mode);

        return runId;
    }

    public IReadOnlyList<DemoRunSummary> GetRecentRuns()
    {
        return _runs.Recent(10)
            .Select(run => new DemoRunSummary(
                run.RunId,
                run.Mode.ToString(),
                run.LastPhase ?? FallbackPhase(run),
                run.AwaitingAck,
                run.Cancelled,
                run.RequestedAtUtc))
            .ToList();
    }

    private static string FallbackPhase(DemoRun run)
    {
        if (run.Cancelled)
        {
            return "cancelled";
        }

        if (run.AwaitingAck)
        {
            return "pending-ack";
        }

        return "running";
    }

    public async Task<DemoAckResult> AcknowledgeRunAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var run = _runs.Get(runId)
            ?? throw new InvalidOperationException($"Unknown demo run '{runId}'.");

        if (run.Mode != DemoMode.Interactive)
        {
            return new DemoAckResult(
                runId,
                Acknowledged: false,
                AlreadyAcknowledged: false,
                Cancelled: false,
                AcknowledgedAtUtc: null,
                AckWaitMs: null,
                Message: "Automatic runs do not require acknowledgment.");
        }

        if (run.Cancelled)
        {
            return new DemoAckResult(
                runId,
                Acknowledged: false,
                AlreadyAcknowledged: false,
                Cancelled: true,
                AcknowledgedAtUtc: null,
                AckWaitMs: null,
                Message: "The run was already ignored.");
        }

        var alreadyAcknowledged = run.AcknowledgedAtUtc is not null;

        if (!alreadyAcknowledged)
        {
            run.AcknowledgedAtUtc = DateTime.UtcNow;
            run.AwaitingAck = false;

            run.AckGate.TrySetResult(DemoAckDecision.Upload);

            using var scope = _scopeFactory.CreateScope();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var ringRepository =
                scope.ServiceProvider.GetRequiredService<IDoorbellRingEventRepository>();

            var ring = await ringRepository.GetByDeviceEventIdAsync(
                run.DeviceId,
                runId,
                cancellationToken);

            if (ring is not null)
            {
                run.RingEventId = ring.Id;

                await sender.Send(
                    new AcknowledgeRingCommand(ring.Id),
                    cancellationToken);
            }

            _logger.LogInformation(
                "Demo: run {RunId} acknowledged at {AckUtc}.",
                runId,
                run.AcknowledgedAtUtc);
        }

        return new DemoAckResult(
            runId,
            Acknowledged: true,
            AlreadyAcknowledged: alreadyAcknowledged,
            Cancelled: false,
            AcknowledgedAtUtc: run.AcknowledgedAtUtc,
            AckWaitMs: Ms(run.RequestedAtUtc, run.AcknowledgedAtUtc),
            Message: alreadyAcknowledged
                ? "The run was already acknowledged."
                : "Run acknowledged; capturing the snapshot.");
    }

    public async Task<DemoAckResult> RejectRunAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var run = _runs.Get(runId)
            ?? throw new InvalidOperationException($"Unknown demo run '{runId}'.");

        if (run.Cancelled)
        {
            return new DemoAckResult(
                runId,
                Acknowledged: false,
                AlreadyAcknowledged: false,
                Cancelled: true,
                AcknowledgedAtUtc: null,
                AckWaitMs: null,
                Message: "The run was already ignored.");
        }

        run.Cancelled = true;
        run.AwaitingAck = false;
        run.RejectedAtUtc = DateTime.UtcNow;

        run.AckGate.TrySetResult(DemoAckDecision.Discard);

        using (var scope = _scopeFactory.CreateScope())
        {
            var ringRepository =
                scope.ServiceProvider.GetRequiredService<IDoorbellRingEventRepository>();

            var commandRepository =
                scope.ServiceProvider.GetRequiredService<IDeviceCommandRepository>();

            var unitOfWork =
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var ring = await ringRepository.GetByDeviceEventIdAsync(
                run.DeviceId,
                runId,
                cancellationToken);

            if (ring is not null)
            {
                run.RingEventId = ring.Id;
                var changed = false;

                if (ring.AcknowledgeStatus == AcknowledgeStatus.Pending)
                {
                    ring.MarkMissed();
                    changed = true;
                }

                if (ring.SnapshotStatus == SnapshotStatus.Requested)
                {
                    ring.MarkSnapshotFailed();
                    changed = true;
                }

                var snapshotCommand = await commandRepository.GetByRingEventIdAsync(
                    ring.Id,
                    DeviceCommandType.Snapshot,
                    cancellationToken);

                if (snapshotCommand is not null
                    && snapshotCommand.Status == DeviceCommandStatus.Sent)
                {
                    snapshotCommand.MarkFailed("Rejected by the user.");
                    changed = true;
                }

                if (changed)
                {
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
        }

        _logger.LogInformation("Demo: run {RunId} ignored by the user.", runId);

        return new DemoAckResult(
            runId,
            Acknowledged: false,
            AlreadyAcknowledged: false,
            Cancelled: true,
            AcknowledgedAtUtc: null,
            AckWaitMs: null,
            Message: "Run ignored; no snapshot was captured.");
    }

    public async Task<DemoStatusResponse> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var run = _runs.Get(runId);

        var mode = run?.Mode ?? DemoMode.Auto;

        var fixture = await EnsureFixtureAsync(mode, cancellationToken);

        var assets = BuildAssets();

        using var scope = _scopeFactory.CreateScope();

        var ringRepository =
            scope.ServiceProvider.GetRequiredService<IDoorbellRingEventRepository>();

        var commandRepository =
            scope.ServiceProvider.GetRequiredService<IDeviceCommandRepository>();

        var outboxRepository =
            scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var userRepository =
            scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var storage =
            scope.ServiceProvider.GetRequiredService<IObjectStorage>();

        var localStorage =
            scope.ServiceProvider.GetRequiredService<LocalObjectStorage>();

        var ring = await ringRepository.GetByDeviceEventIdAsync(
            fixture.DeviceId,
            runId,
            cancellationToken);

        if (ring is null)
        {
            var pendingPhase = run is { Cancelled: true } ? "cancelled" : "running";

            if (run is not null)
            {
                run.LastPhase = pendingPhase;
            }

            return new DemoStatusResponse(
                runId,
                mode.ToString(),
                EventExists: false,
                RingEventId: null,
                SnapshotStatus: null,
                ChimeStatus: null,
                SnapshotCommand: null,
                ChimeCommand: null,
                RingAlert: null,
                SnapshotDelivery: null,
                Notification: null,
                AcknowledgeStatus: null,
                ImageUrl: null,
                Phase: pendingPhase,
                RunCompleted: run is { Cancelled: true },
                AwaitingAck: run?.AwaitingAck ?? false,
                Cancelled: run?.Cancelled ?? false,
                AcknowledgedAtUtc: run?.AcknowledgedAtUtc,
                AckWaitMs: Ms(run?.RequestedAtUtc, run?.AcknowledgedAtUtc),
                SnapshotMeta: new DemoSnapshotMeta(null, null, null, null, null),
                Timeline: BuildTimeline(
                    run,
                    ring: null,
                    snapshot: null,
                    chime: null,
                    ringAlert: null,
                    snapshotDelivery: null,
                    snapshotMeta: null),
                Assets: assets);
        }

        if (run is not null)
        {
            run.RingEventId = ring.Id;
        }

        var snapshot = await commandRepository.GetByRingEventIdAsync(
            ring.Id,
            DeviceCommandType.Snapshot,
            cancellationToken);

        var chime = await commandRepository.GetByRingEventIdAsync(
            ring.Id,
            DeviceCommandType.Chime,
            cancellationToken);

        var outboxes = await outboxRepository.GetByRingEventIdAsync(
            ring.Id,
            cancellationToken);

        var ringAlertEntity = outboxes.FirstOrDefault(
            outbox => outbox.Kind == NotificationKind.RingAlert);

        var snapshotDeliveryEntity = outboxes.FirstOrDefault(
            outbox => outbox.Kind == NotificationKind.SnapshotReady);

        var ringAlertStatus = await BuildOutboxStatusAsync(
            outboxRepository,
            userRepository,
            ringAlertEntity,
            cancellationToken);

        var snapshotDeliveryStatus = await BuildOutboxStatusAsync(
            outboxRepository,
            userRepository,
            snapshotDeliveryEntity,
            cancellationToken);

        string? imageUrl = null;

        if (ring.SnapshotStatus == SnapshotStatus.Captured
            && !string.IsNullOrWhiteSpace(ring.SnapshotStorageKey))
        {
            try
            {
                var uri = await storage.CreateDownloadUrlAsync(
                    ring.SnapshotStorageKey,
                    TimeSpan.FromMinutes(5),
                    cancellationToken);

                imageUrl = uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Demo: failed to build signed image URL for run {RunId}.",
                    runId);
            }
        }

        var snapshotMeta = BuildSnapshotMeta(ring, localStorage);

        var phase = BuildPhase(
            ring,
            snapshotDeliveryEntity,
            _options.Value.MaxAttempts,
            run);

        if (run is not null)
        {
            run.LastPhase = phase;
        }

        return new DemoStatusResponse(
            runId,
            mode.ToString(),
            EventExists: true,
            RingEventId: ring.Id.ToString(),
            SnapshotStatus: ring.SnapshotStatus.ToString(),
            ChimeStatus: ring.ChimeStatus.ToString(),
            SnapshotCommand: snapshot is null
                ? null
                : new DemoCommandStatus(
                    snapshot.RequestId,
                    snapshot.Status.ToString(),
                    snapshot.Attempt,
                    snapshot.StorageKey),
            ChimeCommand: chime is null
                ? null
                : new DemoCommandStatus(
                    chime.RequestId,
                    chime.Status.ToString(),
                    chime.Attempt,
                    chime.StorageKey),
            RingAlert: ringAlertStatus,
            SnapshotDelivery: snapshotDeliveryStatus,
            Notification: PickLatestNotification(
                ringAlertStatus,
                snapshotDeliveryStatus),
            AcknowledgeStatus: ring.AcknowledgeStatus.ToString(),
            ImageUrl: imageUrl,
            Phase: phase,
            RunCompleted: phase is "complete" or "failed" or "cancelled",
            AwaitingAck: run?.AwaitingAck ?? false,
            Cancelled: run?.Cancelled ?? false,
            AcknowledgedAtUtc: run?.AcknowledgedAtUtc ?? ring.AcknowledgedAtUtc,
            AckWaitMs: Ms(run?.RequestedAtUtc, run?.AcknowledgedAtUtc ?? ring.AcknowledgedAtUtc),
            SnapshotMeta: snapshotMeta,
            Timeline: BuildTimeline(
                run,
                ring,
                snapshot,
                chime,
                ringAlertStatus,
                snapshotDeliveryStatus,
                snapshotMeta),
            Assets: assets);
    }

    private async Task<DemoFixture> EnsureFixtureAsync(
        DemoMode mode,
        CancellationToken cancellationToken)
    {
        var cached = GetFixture(mode);

        if (cached is not null)
        {
            return cached;
        }

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            cached = GetFixture(mode);

            if (cached is not null)
            {
                return cached;
            }

            var recipientUserId =
                await _recipients.GetRecipientUserIdAsync(cancellationToken);

            using var scope = _scopeFactory.CreateScope();

            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            var deviceId = await sender.Send(
                new CreateDeviceCommand(
                    recipientUserId,
                    DeviceType: "AdvancedDoorbell"),
                cancellationToken);

            var suffix = Guid.NewGuid().ToString("N")[..8];

            var snapshotTimeout = mode == DemoMode.Interactive
                ? _options.Value.InteractiveSnapshotTimeoutSeconds
                : _options.Value.SnapshotTimeoutSeconds;

            await sender.Send(
                new ConfigureDoorbellCommand(
                    deviceId,
                    RingButtonEntityCode: "demo-ring-" + suffix,
                    CameraEntityCode: $"demo-camera-{suffix}",
                    ChimeEntityCode: $"demo-chime-{suffix}",
                    DefaultChimeMelodyId: "demo-melody",
                    SnapshotTimeoutSeconds: snapshotTimeout),
                cancellationToken);

            var fixture = new DemoFixture
            {
                DeviceId = deviceId,
                CameraEntityCode = $"demo-camera-{suffix}",
                ChimeEntityCode = $"demo-chime-{suffix}",
                MelodyId = "demo-melody"
            };

            SetFixture(mode, fixture);

            _logger.LogInformation(
                "Demo: created {Mode} fixture device {DeviceId} (snapshot timeout {Timeout}s).",
                mode,
                fixture.DeviceId,
                snapshotTimeout);

            return fixture;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private DemoFixture? GetFixture(DemoMode mode)
    {
        return mode == DemoMode.Interactive
            ? _interactiveFixture
            : _autoFixture;
    }

    private void SetFixture(DemoMode mode, DemoFixture fixture)
    {
        if (mode == DemoMode.Interactive)
        {
            _interactiveFixture = fixture;
        }
        else
        {
            _autoFixture = fixture;
        }
    }

    private DemoAssetStatus BuildAssets()
    {
        var missing = new List<string>();

        var soundPresent = File.Exists(
            Path.Combine(
                _environment.ContentRootPath,
                _options.Value.SoundRelativePath));

        if (!soundPresent)
        {
            missing.Add(_options.Value.SoundRelativePath);
        }

        var samplePresent = File.Exists(
            Path.Combine(
                _environment.ContentRootPath,
                _options.Value.SampleImageRelativePath));

        if (!samplePresent)
        {
            missing.Add(_options.Value.SampleImageRelativePath);
        }

        return new DemoAssetStatus(
            soundPresent,
            samplePresent,
            missing.ToArray());
    }

    private async Task<DemoOutboxStatus?> BuildOutboxStatusAsync(
        IOutboxRepository outboxRepository,
        IUserRepository userRepository,
        NotificationOutbox? outbox,
        CancellationToken cancellationToken)
    {
        if (outbox is null)
        {
            return null;
        }

        var attempts = await outboxRepository.GetAttemptsAsync(
            outbox.Id,
            cancellationToken);

        var lastAttempt = attempts.Count > 0 ? attempts[^1] : null;

        var recipient = await userRepository.GetByIdAsync(
            outbox.RecipientUserId,
            cancellationToken);

        return new DemoOutboxStatus(
            outbox.Kind.ToString(),
            outbox.RecipientUserId.ToString(),
            recipient?.Name,
            outbox.Status.ToString(),
            outbox.AttemptCount,
            lastAttempt?.Channel?.ToString(),
            lastAttempt?.UsedFallback ?? false,
            lastAttempt?.AttemptedAtUtc,
            lastAttempt?.ErrorMessage,
            Exhausted: outbox.Status == NotificationStatus.Failed
                && outbox.AttemptCount >= _options.Value.MaxAttempts);
    }

    private static DemoSnapshotMeta BuildSnapshotMeta(
        DoorbellRingEvent ring,
        LocalObjectStorage storage)
    {
        if (ring.SnapshotStatus != SnapshotStatus.Captured
            || string.IsNullOrWhiteSpace(ring.SnapshotStorageKey))
        {
            return new DemoSnapshotMeta(null, null, null, null, null);
        }

        long? sizeBytes = null;

        try
        {
            var path = storage.GetFullPath(ring.SnapshotStorageKey);

            if (File.Exists(path))
            {
                sizeBytes = new FileInfo(path).Length;
            }
        }
        catch
        {
        }

        return new DemoSnapshotMeta(
            ring.SnapshotStorageKey,
            ring.SnapshotResolution,
            ring.SnapshotCapturedAtUtc,
            sizeBytes,
            "image/jpeg");
    }

    private static DemoOutboxStatus? PickLatestNotification(
        DemoOutboxStatus? ringAlert,
        DemoOutboxStatus? snapshotDelivery)
    {
        if (snapshotDelivery?.LastAttemptAtUtc is not null)
        {
            return snapshotDelivery;
        }

        return ringAlert ?? snapshotDelivery;
    }

    private static string BuildPhase(
        DoorbellRingEvent ring,
        NotificationOutbox? snapshotDelivery,
        int maxAttempts,
        DemoRun? run)
    {
        if (run is { Cancelled: true })
        {
            return "cancelled";
        }

        if (run is { Mode: DemoMode.Interactive, AwaitingAck: true })
        {
            return "pending-ack";
        }

        var deviceFailed =
            ring.SnapshotStatus is SnapshotStatus.TimedOut
                or SnapshotStatus.Failed
            || ring.ChimeStatus is ChimeStatus.Failed
                or ChimeStatus.Unknown;

        if (deviceFailed)
        {
            return "failed";
        }

        if (snapshotDelivery is not null
            && snapshotDelivery.Status == NotificationStatus.Failed
            && snapshotDelivery.AttemptCount >= maxAttempts)
        {
            return "failed";
        }

        var snapshotTerminal =
            ring.SnapshotStatus == SnapshotStatus.Captured;

        var chimeTerminal =
            ring.ChimeStatus is ChimeStatus.Played
                or ChimeStatus.Failed
                or ChimeStatus.Unknown;

        var notificationTerminal = snapshotDelivery is not null
            && snapshotDelivery.Status == NotificationStatus.Sent;

        return snapshotTerminal && chimeTerminal && notificationTerminal
            ? "complete"
            : "running";
    }

    private static DemoTimelineEvent[] BuildTimeline(
        DemoRun? run,
        DoorbellRingEvent? ring,
        DeviceCommand? snapshot,
        DeviceCommand? chime,
        DemoOutboxStatus? ringAlert,
        DemoOutboxStatus? snapshotDelivery,
        DemoSnapshotMeta? snapshotMeta)
    {
        var interactive = run is { Mode: DemoMode.Interactive };

        var cancelled = run is { Cancelled: true };

        var acknowledged = run?.AcknowledgedAtUtc is not null;

        var events = new List<DemoTimelineEvent>();

        events.Add(new DemoTimelineEvent(
            "ring.published",
            "MQTT Ring Event Published",
            run is null ? "pending" : "done",
            run?.RequestedAtUtc,
            null,
            run is null ? null : $"event {run.RunId}"));

        events.Add(new DemoTimelineEvent(
            "consumer.received",
            "Consumer Received Ring & Triggered Chime",
            ring is null
                ? (run is null ? "pending" : "running")
                : chime is null
                    ? "running"
                    : chime.Status == DeviceCommandStatus.Completed
                        ? "done"
                        : "running",
            ring?.CreatedAtUtc,
            null,
            chime is null
                ? null
                : $"chime {chime.Status}"));

        events.Add(new DemoTimelineEvent(
            "notification.ringAlert",
            "Notification Sent via " + ChannelLabel(ringAlert),
            OutboxState(ringAlert, run is null ? "pending" : "running"),
            ringAlert?.LastAttemptAtUtc,
            null,
            "Doorbell Ringing \u2014 Action Required"));

        var waitingState = !interactive
            ? "skipped"
            : cancelled || acknowledged
                ? "done"
                : run is { AwaitingAck: true }
                    ? "running"
                    : "pending";

        events.Add(new DemoTimelineEvent(
            "ack.waiting",
            "Waiting for User Acknowledgment\u2026",
            waitingState,
            ringAlert?.LastAttemptAtUtc,
            null,
            interactive ? null : "automatic pipeline \u2014 no human gate"));

        events.Add(new DemoTimelineEvent(
            "ack.decided",
            !interactive
                ? "User Acknowledged Ring"
                : cancelled
                    ? "Ring Ignored / Rejected"
                    : "User Acknowledged Ring",
            !interactive
                ? "skipped"
                : cancelled
                    ? "cancelled"
                    : acknowledged
                        ? "done"
                        : "pending",
            run?.AcknowledgedAtUtc ?? run?.RejectedAtUtc,
            null,
            !interactive
                ? null
                : cancelled
                    ? "snapshot discarded"
                    : acknowledged
                        ? "ack gate released \u00B7 mock camera resumed"
                        : null));

        string? snapshotState;
        string? snapshotDetail;

        if (cancelled)
        {
            snapshotState = "skipped";
            snapshotDetail = "snapshot discarded";
        }
        else if (ring is null)
        {
            snapshotState = run is null ? "pending" : "running";
            snapshotDetail = null;
        }
        else if (ring.SnapshotStatus == SnapshotStatus.Captured)
        {
            snapshotState = "done";
            snapshotDetail = snapshotMeta?.Resolution is null
                ? $"key {snapshotMeta?.StorageKey ?? snapshot?.StorageKey}"
                : $"{snapshotMeta.Resolution} \u00B7 {FormatBytes(snapshotMeta.SizeBytes)}";
        }
        else if (ring.SnapshotStatus is SnapshotStatus.Failed or SnapshotStatus.TimedOut)
        {
            snapshotState = "failed";
            snapshotDetail = "capture failed";
        }
        else
        {
            snapshotState = "running";
            snapshotDetail = snapshot?.StorageKey is null
                ? null
                : $"key {snapshot.StorageKey}";
        }

        events.Add(new DemoTimelineEvent(
            "snapshot.uploaded",
            "Signed URL Generated & Snapshot Uploaded",
            snapshotState,
            ring?.SnapshotCapturedAtUtc ?? snapshot?.SentAtUtc,
            null,
            snapshotDetail));

        events.Add(new DemoTimelineEvent(
            "notification.snapshotReady",
            "Outbox Processed & Snapshot Delivered \u2014 Complete",
            OutboxState(
                snapshotDelivery,
                cancelled ? "skipped" : run is null ? "pending" : "running"),
            snapshotDelivery?.LastAttemptAtUtc,
            null,
            snapshotDelivery is null
                ? null
                : $"{ChannelLabel(snapshotDelivery)}\u00B7 attempts {snapshotDelivery.AttemptCount}"));

        for (var i = 1; i < events.Count; i++)
        {
            events[i] = events[i] with
            {
                DurationMs = Ms(events[i - 1].AtUtc, events[i].AtUtc)
            };
        }

        return events.ToArray();
    }

    private static string OutboxState(DemoOutboxStatus? outbox, string fallback)
    {
        if (outbox is null)
        {
            return fallback;
        }

        return outbox.Status switch
        {
            nameof(NotificationStatus.Sent) => "done",
            nameof(NotificationStatus.Processing) => "running",
            nameof(NotificationStatus.Failed) => outbox.Exhausted ? "failed" : "running",
            _ => "pending"
        };
    }

    private static string ChannelLabel(DemoOutboxStatus? outbox)
    {
        if (outbox is null || outbox.LastChannel is null)
        {
            return "SignalR / FCM";
        }

        var channel = string.Equals(
            outbox.LastChannel,
            nameof(NotificationChannel.Fcm),
            StringComparison.OrdinalIgnoreCase)
            ? "FCM"
            : "SignalR";

        return outbox.LastUsedFallback
            ? channel + " (Fallback)"
            : channel;
    }

    private static string FormatBytes(long? sizeBytes)
    {
        if (sizeBytes is null)
        {
            return "unknown size";
        }

        if (sizeBytes < 1024)
        {
            return $"{sizeBytes} B";
        }

        if (sizeBytes < 1024 * 1024)
        {
            return $"{sizeBytes / 1024.0:0.#} KB";
        }

        return $"{sizeBytes / (1024.0 * 1024.0):0.##} MB";
    }

    private static long? Ms(DateTime? from, DateTime? to)
    {
        return from is not null && to is not null && to >= from
            ? (long)(to.Value - from.Value).TotalMilliseconds
            : null;
    }
}