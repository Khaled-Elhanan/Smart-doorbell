using System.Text.Json.Serialization;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Models;

public sealed record ChimeAckMqttMessage(
    [property: JsonPropertyName("requestId")] string RequestId);