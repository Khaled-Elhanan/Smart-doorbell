using Doorbell.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Entities
{
    public sealed class AdvancedDoorbellComposition
    {
        public Guid Id { get; private set; }

        public Guid DeviceId { get; private set; }

        public string RingButtonEntityCode { get; private set; } = null!;

        public string CameraEntityCode { get; private set; } = null!;

        public string ChimeEntityCode { get; private set; } = null!;

        public string DefaultChimeMelodyId { get; private set; } = null!;

        public int SnapshotTimeoutSeconds { get; private set; }

        public string RingTopic =>
            $"devices/{DeviceId}/doorbell/ring";

        public string SnapshotCommandTopic =>
            $"devices/{DeviceId}/camera/{CameraEntityCode}/snapshot/command";

        public string SnapshotResultTopic =>
            $"devices/{DeviceId}/camera/{CameraEntityCode}/snapshot/result";

        public string ChimeCommandTopic =>
            $"devices/{DeviceId}/chime/{ChimeEntityCode}/play/command";

        public string ChimeAckTopic =>
            $"devices/{DeviceId}/chime/{ChimeEntityCode}/play/ack";

        public AdvancedDoorbellComposition(
            Guid deviceId,
            string ringButtonEntityCode,
            string cameraEntityCode,
            string chimeEntityCode,
            string defaultChimeMelodyId,
            int snapshotTimeoutSeconds = 8)
        {
            if (deviceId == Guid.Empty)
                throw new DomainException("DeviceId cannot be empty.");

            if (string.IsNullOrWhiteSpace(ringButtonEntityCode))
                throw new DomainException("Ring button entity code is required.");

            if (string.IsNullOrWhiteSpace(cameraEntityCode))
                throw new DomainException("Camera entity code is required.");

            if (string.IsNullOrWhiteSpace(chimeEntityCode))
                throw new DomainException("Chime entity code is required.");

            if (string.IsNullOrWhiteSpace(defaultChimeMelodyId))
                throw new DomainException("Default chime melody id is required.");

            if (snapshotTimeoutSeconds <= 0)
                throw new DomainException(
                    "Snapshot timeout must be greater than zero.");

            Id = Guid.NewGuid();
            DeviceId = deviceId;
            RingButtonEntityCode = ringButtonEntityCode;
            CameraEntityCode = cameraEntityCode;
            ChimeEntityCode = chimeEntityCode;
            DefaultChimeMelodyId = defaultChimeMelodyId;
            SnapshotTimeoutSeconds = snapshotTimeoutSeconds;
        }
    }
    }
