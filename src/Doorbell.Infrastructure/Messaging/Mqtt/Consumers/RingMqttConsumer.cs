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
    private readonly MqttDeadLetterPublisher _deadLetter;
    private readonly MqttOptions _options;
    private readonly ILogger<RingMqttConsumer> _logger;

    public RingMqttConsumer(
        IServiceScopeFactory scopeFactory,
        MqttConnectionManager connectionManager,
        MqttDeadLetterPublisher deadLetter,
        IOptions<MqttOptions> options,
        ILogger<RingMqttConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionManager = connectionManager;
        _deadLetter = deadLetter;
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

        var payload =
            args.ApplicationMessage.Payload.ToArray();

        RingMqttMessage? message;

        try
        {
            message =
                JsonSerializer.Deserialize<RingMqttMessage>(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Invalid ring MQTT payload on {Topic}; dead-lettering.",
                args.ApplicationMessage.Topic);

            await _deadLetter.PublishAsync(
                args.ApplicationMessage.Topic,
                payload,
                CancellationToken.None);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            return;
        }

        if (message is null)
        {
            _logger.LogWarning(
                "Received empty ring MQTT message on {Topic}; dead-lettering.",
                args.ApplicationMessage.Topic);

            await _deadLetter.PublishAsync(
                args.ApplicationMessage.Topic,
                payload,
                CancellationToken.None);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            return;
        }

        var topicDeviceId = ExtractDeviceIdFromTopic(
            args.ApplicationMessage.Topic);

        if (topicDeviceId is null ||
            !string.Equals(
                topicDeviceId,
                message.DeviceId.ToString("D"),
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Ring payload deviceId {PayloadDeviceId} does not match topic deviceId {TopicDeviceId} on {Topic}; rejected and dead-lettered.",
                message.DeviceId,
                topicDeviceId ?? "<missing>",
                args.ApplicationMessage.Topic);

            await _deadLetter.PublishAsync(
                args.ApplicationMessage.Topic,
                payload,
                CancellationToken.None);

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

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process ring MQTT message on {Topic}.",
                args.ApplicationMessage.Topic);

            args.ProcessingFailed = true;
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
            _logger.LogWarning(
                ex,
                "Concurrent orchestration for ring event {RingEventId}; intent already recorded.",
                ringEventId);
        }
    }

    private static string? ExtractDeviceIdFromTopic(string topic)
    {
        var parts = topic.Split('/');

        return parts.Length >= 2 ? parts[1] : null;
    }
}