using System.Text.Json.Serialization;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Models;

public sealed record SnapshotResultMqttMessage(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("resolution")] string Resolution,
    [property: JsonPropertyName("capturedAt")] DateTime CapturedAt);
