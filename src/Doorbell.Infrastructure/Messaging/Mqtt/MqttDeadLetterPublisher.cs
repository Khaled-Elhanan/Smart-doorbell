using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;

namespace Doorbell.Infrastructure.Messaging.Mqtt;

public sealed class MqttDeadLetterPublisher
{
    private readonly MqttConnectionManager _connectionManager;
    private readonly ILogger<MqttDeadLetterPublisher> _logger;

    public MqttDeadLetterPublisher(
        MqttConnectionManager connectionManager,
        ILogger<MqttDeadLetterPublisher> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task PublishAsync(
        string originalTopic,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var deadLetterTopic = $"{originalTopic}/dlq";

        try
        {
            await _connectionManager.EnsureConnectedAsync(
                cancellationToken);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(deadLetterTopic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(
                    MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            var result =
                await _connectionManager.Client.PublishAsync(
                    message,
                    cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Dead-letter publish to {Topic} was rejected: {Reason}.",
                    deadLetterTopic,
                    result.ReasonCode);
            }
        }
        catch (Exception ex)
        {
            // Best-effort only; the failed message is already logged.
            _logger.LogError(
                ex,
                "Failed to dead-letter message from {Topic}.",
                originalTopic);
        }
    }
}