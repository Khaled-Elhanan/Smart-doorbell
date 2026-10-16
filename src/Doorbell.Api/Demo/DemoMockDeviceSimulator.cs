using System.Buffers;
using System.Net.Http.Headers;
using System.Text.Json;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Infrastructure.Messaging.Mqtt;
using Doorbell.Infrastructure.Messaging.Mqtt.Models;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Doorbell.Api.Demo;

public sealed class DemoMockDeviceSimulator : BackgroundService
{
    private const string SnapshotCommandFilter =
        "devices/+/camera/+/snapshot/command";

    private const string ChimeCommandFilter =
        "devices/+/chime/+/play/command";

    private const string SnapshotRequestSuffix = ":snapshot";

    private sealed record SnapshotCommandInvocation(
        [property: System.Text.Json.Serialization.JsonPropertyName("requestId")]
        string RequestId,
        [property: System.Text.Json.Serialization.JsonPropertyName("deviceId")]
        string DeviceId,
        [property: System.Text.Json.Serialization.JsonPropertyName("uploadUrl")]
        string UploadUrl);

    private sealed record ChimeCommandInvocation(
        [property: System.Text.Json.Serialization.JsonPropertyName("requestId")]
        string RequestId);

    private readonly MqttConnectionManager _connectionManager;
    private readonly IMqttCommandPublisher _publisher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<DemoOptions> _options;
    private readonly IWebHostEnvironment _environment;
    private readonly DemoRunStore _runs;
    private readonly ILogger<DemoMockDeviceSimulator> _logger;

    public DemoMockDeviceSimulator(
        MqttConnectionManager connectionManager,
        IMqttCommandPublisher publisher,
        IHttpClientFactory httpClientFactory,
        IOptions<DemoOptions> options,
        IWebHostEnvironment environment,
        DemoRunStore runs,
        ILogger<DemoMockDeviceSimulator> logger)
    {
        _connectionManager = connectionManager;
        _publisher = publisher;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _environment = environment;
        _runs = runs;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _connectionManager.Client.ApplicationMessageReceivedAsync +=
            HandleMessageAsync;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connectionManager.SubscribeAsync(
                    SnapshotCommandFilter,
                    stoppingToken);

                await _connectionManager.SubscribeAsync(
                    ChimeCommandFilter,
                    stoppingToken);

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Demo mock device error.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(
        MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;

        try
        {
            if (MqttTopicFilter.IsMatch(
                    topic,
                    SnapshotCommandFilter))
            {
                await HandleSnapshotCommandAsync(args);

                return;
            }

            if (MqttTopicFilter.IsMatch(
                    topic,
                    ChimeCommandFilter))
            {
                await HandleChimeCommandAsync(args);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Demo mock device failed to handle {Topic}.",
                topic);

            await args.AcknowledgeAsync(
                CancellationToken.None);
        }
    }

    private async Task HandleSnapshotCommandAsync(
        MqttApplicationMessageReceivedEventArgs args)
    {
        args.AutoAcknowledge = false;

        var payload = args.ApplicationMessage.Payload.ToArray();

        SnapshotCommandInvocation invocation;

        try
        {
            invocation =
                JsonSerializer.Deserialize<SnapshotCommandInvocation>(
                    payload)
                ?? throw new InvalidOperationException(
                    "Snapshot command payload is empty.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Demo mock device: invalid snapshot command payload on {Topic}.",
                args.ApplicationMessage.Topic);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            return;
        }

        var resultTopic = args.ApplicationMessage.Topic.Replace(
            "/snapshot/command",
            "/snapshot/result",
            StringComparison.Ordinal);

        var run = _runs.Get(ExtractRunId(invocation.RequestId));

        if (run is null)
        {
            _logger.LogWarning(
                "Demo mock camera: no local run for {RequestId}; ignoring the snapshot command.",
                invocation.RequestId);

            await args.AcknowledgeAsync(CancellationToken.None);

            return;
        }

        if (run is { Cancelled: true })
        {
            _logger.LogInformation(
                "Demo mock camera: run {RunId} was rejected; discarding the snapshot command.",
                run.RunId);

            await args.AcknowledgeAsync(CancellationToken.None);

            return;
        }

        if (run is { Mode: DemoMode.Interactive })
        {
            _logger.LogInformation(
                "Demo mock camera: holding snapshot for run {RunId} pending acknowledgment.",
                run.RunId);

            await args.AcknowledgeAsync(CancellationToken.None);

            _ = Task.Run(() => ResumeInteractiveAsync(run, invocation, resultTopic));

            return;
        }

        await UploadAndPublishAsync(invocation, resultTopic, CancellationToken.None);

        await args.AcknowledgeAsync(CancellationToken.None);
    }

    private async Task ResumeInteractiveAsync(
        DemoRun run,
        SnapshotCommandInvocation invocation,
        string resultTopic)
    {
        try
        {
            var decision = await run.AckGate.Task;

            if (decision != DemoAckDecision.Upload)
            {
                _logger.LogInformation(
                    "Demo mock camera: run {RunId} was not acknowledged; no snapshot captured.",
                    run.RunId);

                return;
            }

            await UploadAndPublishAsync(
                invocation,
                resultTopic,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Demo mock camera: interactive snapshot resume failed for {RequestId}.",
                invocation.RequestId);
        }
    }

    private async Task UploadAndPublishAsync(
        SnapshotCommandInvocation invocation,
        string resultTopic,
        CancellationToken cancellationToken)
    {
        var samplePath = Path.Combine(
            _environment.ContentRootPath,
            _options.Value.SampleImageRelativePath);

        if (!File.Exists(samplePath))
        {
            _logger.LogWarning(
                "Demo mock camera: sample image not found at {Path}; " +
                "no upload, no result (snapshot will time out).",
                samplePath);

            return;
        }

        var imageBytes = await File.ReadAllBytesAsync(
            samplePath,
            cancellationToken);

        using var http = _httpClientFactory.CreateClient("DemoMockDevice");

        using var content = new ByteArrayContent(imageBytes);

        content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        using var response = await http.PutAsync(
            invocation.UploadUrl,
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "Demo mock camera: uploaded {Length} bytes for {RequestId}.",
            imageBytes.Length,
            invocation.RequestId);

        await _publisher.PublishAsync(
            resultTopic,
            new SnapshotResultMqttMessage(
                invocation.RequestId,
                _options.Value.SnapshotResolution,
                DateTime.UtcNow),
            cancellationToken);
    }

    private async Task HandleChimeCommandAsync(
        MqttApplicationMessageReceivedEventArgs args)
    {
        args.AutoAcknowledge = false;

        var payload = args.ApplicationMessage.Payload.ToArray();

        ChimeCommandInvocation invocation;

        try
        {
            invocation =
                JsonSerializer.Deserialize<ChimeCommandInvocation>(
                    payload)
                ?? throw new InvalidOperationException(
                    "Chime command payload is empty.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Demo mock device: invalid chime command payload on {Topic}.",
                args.ApplicationMessage.Topic);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            return;
        }

        var ackTopic = args.ApplicationMessage.Topic.Replace(
            "/play/command",
            "/play/ack",
            StringComparison.Ordinal);

        await _publisher.PublishAsync(
            ackTopic,
            new ChimeAckMqttMessage(invocation.RequestId),
            CancellationToken.None);

        await args.AcknowledgeAsync(
            CancellationToken.None);
    }

    private static string ExtractRunId(string requestId)
    {
        return requestId.EndsWith(SnapshotRequestSuffix, StringComparison.Ordinal)
            ? requestId[..^SnapshotRequestSuffix.Length]
            : requestId;
    }
}