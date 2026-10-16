using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Messaging.Mqtt
{
    public sealed class MqttOptions
    {
        public string Host { get; set; } = "localhost";

        public int Port { get; set; } = 1883;

        public string? ClientId { get; set; }

        public string? Username { get; set; }

        public string? Password { get; set; }

        public bool UseTls { get; set; }

        public string RingTopic { get; set; } =
            "devices/+/doorbell/ring";

        public string SnapshotResultTopic { get; set; } =
            "devices/+/camera/+/snapshot/result";

        public string ChimeAckTopic { get; set; } =
            "devices/+/chime/+/play/ack";

        public int SessionExpirySeconds { get; set; } = 86400;
    }
}