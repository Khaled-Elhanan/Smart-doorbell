using System.Buffers;
using System.Text.Json;
using Doorbell.Application.Features.Doorbell.Commands.OrchestrateDeviceCommands;
using Doorbell.Application.Features.Doorbell.Commands.ReceiveRing;
using Doorbell.Infrastructure.Messaging.Mqtt.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Consumers;

public sealed class RingMqttConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttConnectionManager _connectionManager;
    private readonly MqttOptions _options;
    private readonly ILogger<RingMqttConsumer> _logger;

    public RingMqttConsumer(
        IServiceScopeFactory scopeFactory,
        MqttConnectionManager connectionManager,
        IOptions<MqttOptions> options,
        ILogger<RingMqttConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _options = options.Value;
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
                    _options.RingTopic,
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
                    "MQTT consumer error.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(
        MqttApplicationMessageReceivedEventArgs args)
    {
        if (!MqttTopicFilter.IsMatch(
                args.ApplicationMessage.Topic,
                _options.RingTopic))
        {
            return;
        }

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

            if (result.IsNew)
            {
                await OrchestrateDeviceCommandsAsync(
                    sender,
                    result.RingEventId);
            }

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

    private async Task OrchestrateDeviceCommandsAsync(
        ISender sender,
        Guid ringEventId)
    {
        try
        {
            await sender.Send(
                new OrchestrateDeviceCommandsCommand(ringEventId),
                CancellationToken.None);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent delivery recorded the same intent first; it owns the publish.
            _logger.LogWarning(
                ex,
                "Concurrent orchestration for ring event {RingEventId}; intent already recorded.",
                ringEventId);
        }
    }
}