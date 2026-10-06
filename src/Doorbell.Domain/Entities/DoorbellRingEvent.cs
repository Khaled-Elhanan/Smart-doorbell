using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Entities
{
    public sealed class DoorbellRingEvent
    {
        public Guid Id { get; private set; }

        public Guid DeviceId { get; private set; }

        public string DeviceEventId { get; private set; } = null!;

        public DateTime RingTimestampUtc { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

        // Snapshot
        public SnapshotStatus SnapshotStatus { get; private set; }

        public string? SnapshotStorageKey { get; private set; }

        public string? SnapshotResolution { get; private set; }

        public DateTime? SnapshotCapturedAtUtc { get; private set; }

        // Chime
        public ChimeStatus ChimeStatus { get; private set; }

        public string ChimeMelodyId { get; private set; } = null!;

        // User acknowledgement
        public AcknowledgeStatus AcknowledgeStatus { get; private set; }

        public DateTime? AcknowledgedAtUtc { get; private set; }

        public byte[] RowVersion { get; private set; } = null!;

        private DoorbellRingEvent()
        {
            // EF Core
        }

        private DoorbellRingEvent(
            Guid deviceId,
            string deviceEventId,
            DateTime ringTimestampUtc,
            string chimeMelodyId)
        {
            if (deviceId == Guid.Empty)
                throw new DomainException("DeviceId cannot be empty.");

            if (string.IsNullOrWhiteSpace(deviceEventId))
                throw new DomainException("DeviceEventId is required.");

            if (string.IsNullOrWhiteSpace(chimeMelodyId))
                throw new DomainException("Chime melody id is required.");

            Id = Guid.NewGuid();

            DeviceId = deviceId;
            DeviceEventId = deviceEventId;

            RingTimestampUtc = ringTimestampUtc;
            CreatedAtUtc = DateTime.UtcNow;

            SnapshotStatus = SnapshotStatus.NotRequested;

            ChimeStatus = ChimeStatus.NotRequested;
            ChimeMelodyId = chimeMelodyId;

            AcknowledgeStatus = AcknowledgeStatus.Pending;
        }

        public static DoorbellRingEvent Create(
            Guid deviceId,
            string deviceEventId,
            DateTime ringTimestampUtc,
            string chimeMelodyId)
        {
            return new DoorbellRingEvent(
                deviceId,
                deviceEventId,
                ringTimestampUtc,
                chimeMelodyId);
        }

        public void MarkSnapshotRequested()
        {
            if (SnapshotStatus != SnapshotStatus.NotRequested)
                throw new DomainException(
                    "Snapshot can only be requested from NotRequested state.");

            SnapshotStatus = SnapshotStatus.Requested;
        }

        public void ApplySnapshotCaptured(
            string storageKey,
            string resolution,
            DateTime capturedAtUtc)
        {
            if (SnapshotStatus == SnapshotStatus.Captured)
                throw new DomainException(
                    "Snapshot has already been captured.");

            if (SnapshotStatus == SnapshotStatus.NotRequested)
                throw new DomainException(
                    "Snapshot was never requested for this ring event.");

            if (string.IsNullOrWhiteSpace(storageKey))
                throw new DomainException("Snapshot storage key is required.");

            if (string.IsNullOrWhiteSpace(resolution))
                throw new DomainException("Snapshot resolution is required.");

            SnapshotStorageKey = storageKey;
            SnapshotResolution = resolution;
            SnapshotCapturedAtUtc = capturedAtUtc;

            SnapshotStatus = SnapshotStatus.Captured;
        }

        public void MarkSnapshotTimedOut()
        {
            if (SnapshotStatus != SnapshotStatus.Requested)
                throw new DomainException(
                    "Snapshot can only timeout when it is Requested.");

            SnapshotStatus = SnapshotStatus.TimedOut;
        }

        public void MarkSnapshotFailed()
        {
            if (SnapshotStatus != SnapshotStatus.Requested)
                throw new DomainException(
                    "Snapshot can only fail when it is Requested.");

            SnapshotStatus = SnapshotStatus.Failed;
        }

        public void MarkChimeRequested()
        {
            if (ChimeStatus != ChimeStatus.NotRequested)
                throw new DomainException(
                    "Chime can only be requested from NotRequested state.");

            ChimeStatus = ChimeStatus.Requested;
        }

        public void MarkChimePlayed()
        {
            if (ChimeStatus == ChimeStatus.Played)
                throw new DomainException(
                    "Chime has already been played.");

            if (ChimeStatus == ChimeStatus.NotRequested)
                throw new DomainException(
                    "Chime was never requested for this ring event.");

            ChimeStatus = ChimeStatus.Played;
        }

        public void MarkChimeFailed()
        {
            if (ChimeStatus != ChimeStatus.Requested)
                throw new DomainException(
                    "Chime can only fail when it is Requested.");

            ChimeStatus = ChimeStatus.Failed;
        }

        public void MarkChimeUnknown()
        {
            if (ChimeStatus != ChimeStatus.Requested)
                throw new DomainException(
                    "Chime can only become Unknown when it is Requested.");

            ChimeStatus = ChimeStatus.Unknown;
        }

        public void Acknowledge()
        {
            if (AcknowledgeStatus == AcknowledgeStatus.Acknowledged)
                return;

            AcknowledgeStatus = AcknowledgeStatus.Acknowledged;
            AcknowledgedAtUtc = DateTime.UtcNow;
        }

        public void MarkMissed()
        {
            if (AcknowledgeStatus != AcknowledgeStatus.Pending)
                throw new DomainException(
                    "Only pending events can be marked as missed.");

            AcknowledgeStatus = AcknowledgeStatus.Missed;
        }
    }

}