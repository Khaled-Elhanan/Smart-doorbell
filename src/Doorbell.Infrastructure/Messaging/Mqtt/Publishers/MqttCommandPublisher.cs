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

    private readonly IMqttClient _client;
    private readonly ILogger<MqttCommandPublisher> _logger;

    public MqttCommandPublisher(
        IMqttClient client,
        ILogger<MqttCommandPublisher> logger)
    {
        _client = client;
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

        if (!_client.IsConnected)
        {
            throw new InvalidOperationException(
                "MQTT client is not connected.");
        }

        var json = JsonSerializer.Serialize(
            payload,
            JsonOptions);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(
                MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _client.PublishAsync(
            message,
            cancellationToken);

        _logger.LogInformation(
            "Published MQTT command to {Topic}",
            topic);
    }
}