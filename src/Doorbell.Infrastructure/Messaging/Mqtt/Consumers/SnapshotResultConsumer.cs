using System.Buffers;
using System.Text.Json;
using Doorbell.Application.Features.Doorbell.Commands.HandleSnapshotResult;
using Doorbell.Infrastructure.Messaging.Mqtt.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Consumers;

public sealed class SnapshotResultConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttConnectionManager _connectionManager;
    private readonly MqttOptions _options;
    private readonly ILogger<SnapshotResultConsumer> _logger;

    public SnapshotResultConsumer(
        IServiceScopeFactory scopeFactory,
        MqttConnectionManager connectionManager,
        IOptions<MqttOptions> options,
        ILogger<SnapshotResultConsumer> logger)
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
                    _options.SnapshotResultTopic,
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
                    "Snapshot result consumer error.");

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
                _options.SnapshotResultTopic))
        {
            return;
        }

        args.AutoAcknowledge = false;

        try
        {
            var payload =
                args.ApplicationMessage.Payload.ToArray();

            var message =
                JsonSerializer.Deserialize<SnapshotResultMqttMessage>(
                    payload);

            if (message is null)
            {
                _logger.LogWarning(
                    "Received empty or invalid snapshot result message.");

                await args.AcknowledgeAsync(
                    CancellationToken.None);

                return;
            }

            _logger.LogInformation(
                "Received snapshot result for RequestId {RequestId}",
                message.RequestId);

            using var scope =
                _scopeFactory.CreateScope();

            var sender =
                scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(
                new HandleSnapshotResultCommand(
                    message.RequestId,
                    message.Resolution,
                    message.CapturedAt),
                CancellationToken.None);

            await args.AcknowledgeAsync(
                CancellationToken.None);

            _logger.LogInformation(
                "Successfully processed and acknowledged snapshot result for RequestId {RequestId}",
                message.RequestId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Invalid snapshot result MQTT payload.");

            await args.AcknowledgeAsync(
                CancellationToken.None);
        }
        catch (DbUpdateException ex)
        {
            // A duplicate or concurrent result already applied the same state.
            _logger.LogWarning(
                ex,
                "Concurrent snapshot result; state already applied.");

            await args.AcknowledgeAsync(
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process snapshot result MQTT message.");

            throw;
        }
    }
}
