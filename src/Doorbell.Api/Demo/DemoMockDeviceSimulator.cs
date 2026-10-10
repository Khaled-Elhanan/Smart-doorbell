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
    private readonly ILogger<DemoMockDeviceSimulator> _logger;

    public DemoMockDeviceSimulator(
        MqttConnectionManager connectionManager,
        IMqttCommandPublisher publisher,
        IHttpClientFactory httpClientFactory,
        IOptions<DemoOptions> options,
        IWebHostEnvironment environment,
        ILogger<DemoMockDeviceSimulator> logger)
    {
        _connectionManager = connectionManager;
        _publisher = publisher;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _environment = environment;
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

        var samplePath = Path.Combine(
            _environment.ContentRootPath,
            _options.Value.SampleImageRelativePath);

        if (!File.Exists(samplePath))
        {
            _logger.LogWarning(
                "Demo mock camera: sample image not found at {Path}; " +
                "no upload, no result (snapshot will time out).",
                samplePath);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            return;
        }

        var imageBytes = await File.ReadAllBytesAsync(
            samplePath,
            CancellationToken.None);

        using var http = _httpClientFactory.CreateClient(
            "DemoMockDevice");

        using var content = new ByteArrayContent(imageBytes);

        content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        using var response = await http.PutAsync(
            invocation.UploadUrl,
            content,
            CancellationToken.None);

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
            CancellationToken.None);

        await args.AcknowledgeAsync(
            CancellationToken.None);
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
}