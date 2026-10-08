using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Messaging.Mqtt
{
    public sealed class MqttOptions
    {
        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 1883;

        public string ClientId { get; set; } = "doorbell-backend";

        public string RingTopic { get; set; } =
            "devices/+/doorbell/ring";

        public string SnapshotResultTopic { get; set; } =
            "devices/+/camera/+/snapshot/result";

        public int SessionExpirySeconds { get; set; } = 86400;
    }
}
