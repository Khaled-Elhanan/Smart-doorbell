using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Doorbell.Infrastructure.Messaging.Mqtt.Models
{
    public sealed record RingMqttMessage(
     [property: JsonPropertyName("deviceId")] Guid DeviceId,
     [property: JsonPropertyName("eventId")] string EventId,
     [property: JsonPropertyName("timestamp")] DateTime Timestamp);
}
