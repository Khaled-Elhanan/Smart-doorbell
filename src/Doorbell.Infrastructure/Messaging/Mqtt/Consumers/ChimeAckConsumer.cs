using System.Buffers;
using System.Text.Json;
using Doorbell.Application.Features.Doorbell.Commands.HandleChimeAck;
using Doorbell.Infrastructure.Messaging.Mqtt.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Consumers;

public sealed class ChimeAckConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttConnectionManager _connectionManager;
    private readonly MqttOptions _options;
    private readonly ILogger<ChimeAckConsumer> _logger;

    public ChimeAckConsumer(
        IServiceScopeFactory scopeFactory,
        MqttConnectionManager connectionManager,
        IOptions<MqttOptions> options,
        ILogger<ChimeAckConsumer> logger)
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
                    _options.ChimeAckTopic,
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
                    "Chime ack consumer error.");

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
                _options.ChimeAckTopic))
        {
            return;
        }

        args.AutoAcknowledge = false;

        try
        {
            var payload =
                args.ApplicationMessage.Payload.ToArray();

            var message =
                JsonSerializer.Deserialize<ChimeAckMqttMessage>(
                    payload);

            if (message is null)
            {
                _logger.LogWarning(
                    "Received empty or invalid chime ack message.");

                await args.AcknowledgeAsync(
                    CancellationToken.None);

                return;
            }

            _logger.LogInformation(
                "Received chime ack for RequestId {RequestId}",
                message.RequestId);

            using var scope =
                _scopeFactory.CreateScope();

            var sender =
                scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new HandleChimeAckCommand(
                    message.RequestId),
                CancellationToken.None);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            _logger.LogInformation(
                "Successfully processed and acknowledged chime ack for RequestId {RequestId}",
                message.RequestId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Invalid chime ack MQTT payload.");

            await args.AcknowledgeAsync(
                CancellationToken.None);
        }
        catch (DbUpdateException ex)
        {
            // A duplicate or concurrent ack already applied the same state.
            _logger.LogWarning(
                ex,
                "Concurrent chime ack; state already applied.");

            await args.AcknowledgeAsync(
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process chime ack MQTT message.");

            throw;
        }
    }
}