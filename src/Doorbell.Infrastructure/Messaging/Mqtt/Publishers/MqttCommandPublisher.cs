using System.Text.Json;
using Doorbell.Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Publishers;

public sealed class MqttCommandPublisher : IMqttCommandPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly MqttConnectionManager _connectionManager;
    private readonly ILogger<MqttCommandPublisher> _logger;

    public MqttCommandPublisher(
        MqttConnectionManager connectionManager,
        ILogger<MqttCommandPublisher> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task PublishAsync(
        string topic,
        object payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            throw new ArgumentException(
                "MQTT topic is required.",
                nameof(topic));
        }

        await _connectionManager.EnsureConnectedAsync(
            cancellationToken);

        var json = JsonSerializer.Serialize(
            payload,
            JsonOptions);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(
                MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        var result = await _connectionManager.Client.PublishAsync(
            message,
            cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"MQTT publish to {topic} was rejected: {result.ReasonCode}.");
        }

        _logger.LogInformation(
            "Published MQTT command to {Topic}",
            topic);
    }
}
