using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Abstractions.Storage;
using Doorbell.Application.Features.Devices.Commands.ConfigureDoorbell;
using Doorbell.Application.Features.Devices.Commands.CreateDevice;
using Doorbell.Application.Features.Users.Commands.CreateUser;
using Doorbell.Domain.Enums;
using Doorbell.Infrastructure.Messaging.Mqtt.Models;
using MediatR;
using Microsoft.Extensions.Options;

namespace Doorbell.Api.Demo;

public sealed record DemoCommandStatus(
    string? RequestId,
    string? Status,
    int Attempt,
    string? StorageKey);

public sealed record DemoOutboxStatus(
    string? RecipientUserId,
    string? RecipientName,
    string Status,
    int AttemptCount);

public sealed record DemoAssetStatus(
    bool SoundPresent,
    bool SampleImagePresent,
    string[] Missing);

public sealed record DemoStep(string Label, string State, string? Detail);

public sealed record DemoStatusResponse(
    string RunId,
    bool EventExists,
    string? RingEventId,
    string? SnapshotStatus,
    string? ChimeStatus,
    DemoCommandStatus? SnapshotCommand,
    DemoCommandStatus? ChimeCommand,
    DemoOutboxStatus? Outbox,
    string? ImageUrl,
    string Phase,
    bool RunCompleted,
    DemoStep[] Steps,
    DemoAssetStatus Assets);

public sealed class DemoSimulationService
{
    private sealed class DemoFixture
    {
        public Guid UserId { get; init; }

        public Guid DeviceId { get; init; }

        public string CameraEntityCode { get; init; } = null!;

        public string ChimeEntityCode { get; init; } = null!;

        public string MelodyId { get; init; } = null!;

        public string RingTopic => $"devices/{DeviceId}/doorbell/ring";
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMqttCommandPublisher _publisher;
    private readonly IOptions<DemoOptions> _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DemoSimulationService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private DemoFixture? _fixture;

    public DemoSimulationService(
        IServiceScopeFactory scopeFactory,
        IMqttCommandPublisher publisher,
        IOptions<DemoOptions> options,
        IWebHostEnvironment environment,
        ILogger<DemoSimulationService> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> StartRingAsync(
        CancellationToken cancellationToken)
    {
        var fixture = await EnsureFixtureAsync(cancellationToken);

        var runId = "demo-" + Guid.NewGuid().ToString("N")[..10];

        await _publisher.PublishAsync(
            fixture.RingTopic,
            new RingMqttMessage(
                fixture.DeviceId,
                runId,
                DateTime.UtcNow),
            cancellationToken);

        _logger.LogInformation(
            "Demo: published ring {RunId} for device {DeviceId}.",
            runId,
            fixture.DeviceId);

        return runId;
    }

    public async Task<DemoStatusResponse> GetStatusAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var fixture = await EnsureFixtureAsync(cancellationToken);

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

        var ring = await ringRepository.GetByDeviceEventIdAsync(
            fixture.DeviceId,
            runId,
            cancellationToken);

        if (ring is null)
        {
            return new DemoStatusResponse(
                runId,
                EventExists: false,
                RingEventId: null,
                SnapshotStatus: null,
                ChimeStatus: null,
                SnapshotCommand: null,
                ChimeCommand: null,
                Outbox: null,
                ImageUrl: null,
                Phase: "running",
                RunCompleted: false,
                Steps: BuildSteps(fixture, ring: null, snapshot: null, chime: null, outbox: null),
                Assets: assets);
        }

        var snapshot = await commandRepository.GetByRingEventIdAsync(
            ring.Id,
            DeviceCommandType.Snapshot,
            cancellationToken);

        var chime = await commandRepository.GetByRingEventIdAsync(
            ring.Id,
            DeviceCommandType.Chime,
            cancellationToken);

        var outbox = await outboxRepository.GetByRingEventIdAsync(
            ring.Id,
            cancellationToken);

        string? imageUrl = null;

        if (ring.SnapshotStatus == SnapshotStatus.Captured &&
            !string.IsNullOrWhiteSpace(ring.SnapshotStorageKey))
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

        var steps = BuildSteps(
            fixture,
            ring,
            snapshot,
            chime,
            outbox);

        var phase = BuildPhase(ring, outbox);

        DemoOutboxStatus? outboxStatus = null;

        if (outbox is not null)
        {
            string? recipientName = null;

            var recipient = await userRepository.GetByIdAsync(
                outbox.RecipientUserId,
                cancellationToken);

            if (recipient is not null)
            {
                recipientName = recipient.Name;
            }

            outboxStatus = new DemoOutboxStatus(
                outbox.RecipientUserId.ToString(),
                recipientName,
                outbox.Status.ToString(),
                outbox.AttemptCount);
        }

        return new DemoStatusResponse(
            runId,
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
            Outbox: outboxStatus,
            ImageUrl: imageUrl,
            Phase: phase,
            RunCompleted: phase != "running",
            Steps: steps,
            Assets: assets);
    }

    private async Task<DemoFixture> EnsureFixtureAsync(
        CancellationToken cancellationToken)
    {
        if (_fixture is not null)
        {
            return _fixture;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_fixture is not null)
            {
                return _fixture;
            }

            using var scope = _scopeFactory.CreateScope();

            var sender =
                scope.ServiceProvider.GetRequiredService<ISender>();

            var suffix = Guid.NewGuid().ToString("N")[..8];

            var userId = await sender.Send(
                new CreateUserCommand($"DoorBell Demo ({suffix})"),
                cancellationToken);

            var deviceId = await sender.Send(
                new CreateDeviceCommand(
                    userId,
                    DeviceType: "AdvancedDoorbell"),
                cancellationToken);

            var cameraCode = "demo-camera-" + suffix;
            var chimeCode = "demo-chime-" + suffix;

            await sender.Send(
                new ConfigureDoorbellCommand(
                    deviceId,
                    RingButtonEntityCode: "demo-ring-" + suffix,
                    CameraEntityCode: cameraCode,
                    ChimeEntityCode: chimeCode,
                    DefaultChimeMelodyId: "demo-melody",
                    SnapshotTimeoutSeconds: _options.Value.SnapshotTimeoutSeconds),
                cancellationToken);

            var fixture = new DemoFixture
            {
                UserId = userId,
                DeviceId = deviceId,
                CameraEntityCode = cameraCode,
                ChimeEntityCode = chimeCode,
                MelodyId = "demo-melody"
            };

            _fixture = fixture;

            _logger.LogInformation(
                "Demo: created fixture user {UserId}, device {DeviceId}, camera {CameraCode}, chime {ChimeCode}.",
                fixture.UserId,
                fixture.DeviceId,
                fixture.CameraEntityCode,
                fixture.ChimeEntityCode);

            return _fixture;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private DemoAssetStatus BuildAssets()
    {
        var missing = new List<string>();

        var soundPresent = File.Exists(
            Path.Combine(_environment.ContentRootPath, _options.Value.SoundRelativePath));

        if (!soundPresent)
        {
            var wavFallback = Path.ChangeExtension(
                _options.Value.SoundRelativePath,
                ".wav");

            soundPresent = File.Exists(
                Path.Combine(_environment.ContentRootPath, wavFallback));

            if (!soundPresent)
            {
                missing.Add(_options.Value.SoundRelativePath);
            }
        }

        var samplePresent = File.Exists(
            Path.Combine(_environment.ContentRootPath, _options.Value.SampleImageRelativePath));

        if (!samplePresent)
        {
            missing.Add(_options.Value.SampleImageRelativePath);
        }

        return new DemoAssetStatus(
            soundPresent,
            samplePresent,
            missing.ToArray());
    }

    private static DemoStep[] BuildSteps(
        DemoFixture fixture,
        Doorbell.Domain.Entities.DoorbellRingEvent? ring,
        Doorbell.Domain.Entities.DeviceCommand? snapshot,
        Doorbell.Domain.Entities.DeviceCommand? chime,
        Doorbell.Domain.Entities.NotificationOutbox? outbox)
    {
        var steps = new List<DemoStep>
        {
            new(
                "Ring published to MQTT",
                "done",
                fixture.RingTopic)
        };

        if (ring is null)
        {
            steps.Add(new(
                "Waiting for the ring event to be ingested",
                "running",
                null));

            return steps.ToArray();
        }

        steps.Add(new(
            "Ring event persisted & orchestrated",
            "done",
            $"{ring.DeviceEventId} · requested snapshot + chime"));

        var snapshotSent =
            snapshot is not null && snapshot.SentAtUtc is not null;

        steps.Add(new(
            snapshotSent ? "Snapshot command sent" : "Sending snapshot command",
            snapshotSent ? "done" : "running",
            snapshot is null ? null : $"{snapshot.RequestId}"));

        var (snapshotState, snapshotDetail) = ring.SnapshotStatus switch
        {
            SnapshotStatus.Captured =>
                ("done", $"uploaded · {ring.SnapshotResolution}"),
            SnapshotStatus.TimedOut =>
                ("failed", "no result within the configured timeout"),
            SnapshotStatus.Failed =>
                ("failed", "processing failed"),
            _ => ("running", "waiting for camera upload + result")
        };

        steps.Add(new(
            "Camera upload & snapshot result",
            snapshotState,
            snapshotDetail));

        var chimeSent =
            chime is not null && chime.SentAtUtc is not null;

        steps.Add(new(
            chimeSent ? "Chime command sent" : "Sending chime command",
            chimeSent ? "done" : "running",
            chime is null ? null : $"{chime.RequestId} · melody {ring.ChimeMelodyId}"));

        var (chimeState, chimeDetail) = ring.ChimeStatus switch
        {
            ChimeStatus.Played =>
                ("done", "acknowledged"),
            ChimeStatus.Failed =>
                ("failed", "processing failed"),
            ChimeStatus.Unknown =>
                ("failed", "no ack within the reconcile timeout"),
            _ => ("running", "waiting for chime ack")
        };

        steps.Add(new(
            "Chime acknowledgement",
            chimeState,
            chimeDetail));

        steps.Add(new(
            outbox is null
                ? "Creating notification outbox row"
                : "Notification outbox created (Pending)",
            outbox is null ? "running" : "done",
            outbox is null
                ? null
                : $"recipient {outbox.RecipientUserId} · attempts {outbox.AttemptCount}"));

        return steps.ToArray();
    }

    private static string BuildPhase(
        Doorbell.Domain.Entities.DoorbellRingEvent ring,
        Doorbell.Domain.Entities.NotificationOutbox? outbox)
    {
        var snapshotTerminal =
            ring.SnapshotStatus is SnapshotStatus.Captured
                or SnapshotStatus.TimedOut
                or SnapshotStatus.Failed;

        var chimeTerminal =
            ring.ChimeStatus is ChimeStatus.Played
                or ChimeStatus.Failed
                or ChimeStatus.Unknown;

        var failed =
            ring.SnapshotStatus is SnapshotStatus.TimedOut
                or SnapshotStatus.Failed
            || ring.ChimeStatus is ChimeStatus.Failed
                or ChimeStatus.Unknown;

        if (failed)
        {
            return "failed";
        }

        return snapshotTerminal && chimeTerminal && outbox is not null
            ? "complete"
            : "running";
    }
}