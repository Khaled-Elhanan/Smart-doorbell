using System.Buffers;
using System.Text.Json;
using Doorbell.Application.Features.Doorbell.Commands.ReceiveRing;
using Doorbell.Infrastructure.Messaging.Mqtt.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Consumers;

public sealed class RingMqttConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttOptions _options;
    private readonly ILogger<RingMqttConsumer> _logger;

    private readonly MqttClientFactory _factory = new();

    private IMqttClient? _client;

    public RingMqttConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<MqttOptions> options,
        ILogger<RingMqttConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _client = _factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync +=
            HandleMessageAsync;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await ConnectAsync(stoppingToken);
                }

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
                    "MQTT consumer error.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }

    private async Task ConnectAsync(
        CancellationToken cancellationToken)
    {
        var options = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(
                _options.Host,
                _options.Port)
            .WithCleanStart(false)
            .WithSessionExpiryInterval(
                (uint)_options.SessionExpirySeconds)
            .WithKeepAlivePeriod(
                TimeSpan.FromSeconds(30))
            .Build();

        await _client!.ConnectAsync(
            options,
            cancellationToken);

        var subscribeOptions =
            _factory.CreateSubscribeOptionsBuilder()
                .WithTopicFilter(
                    filter => filter
                        .WithTopic(_options.RingTopic)
                        .WithAtLeastOnceQoS())
                .Build();

        await _client.SubscribeAsync(
            subscribeOptions,
            cancellationToken);

        _logger.LogInformation(
            "Connected to MQTT broker and subscribed to {Topic}",
            _options.RingTopic);
    }

    private async Task HandleMessageAsync(
        MqttApplicationMessageReceivedEventArgs args)
    {
        args.AutoAcknowledge = false;

        try
        {
            var payload =
                args.ApplicationMessage.Payload.ToArray();

            var message =
                JsonSerializer.Deserialize<RingMqttMessage>(
                    payload);

            if (message is null)
            {
                _logger.LogWarning(
                    "Received empty or invalid MQTT message.");

                await args.AcknowledgeAsync(
                    CancellationToken.None);

                return;
            }

            _logger.LogInformation(
                "Received ring event {EventId} for device {DeviceId}",
                message.EventId,
                message.DeviceId);

            var command = new ReceiveRingCommand(
                message.DeviceId,
                message.EventId,
                message.Timestamp);

            using var scope =
                _scopeFactory.CreateScope();

            var sender =
                scope.ServiceProvider.GetRequiredService<ISender>();

            var result = await sender.Send(
                command,
                CancellationToken.None);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            _logger.LogInformation(
                "Successfully processed and acknowledged ring event {EventId}. RingEventId: {RingEventId}. IsNew: {IsNew}",
                message.EventId,
                result.RingEventId,
                result.IsNew);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Invalid ring MQTT payload.");

            await args.AcknowledgeAsync(
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process ring MQTT message.");

            throw;
        }
    }
}