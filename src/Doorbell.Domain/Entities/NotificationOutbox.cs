using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;

namespace Doorbell.Domain.Entities
{
    public sealed class NotificationOutbox
    {
        public Guid Id { get; private set; }

        public Guid RingEventId { get; private set; }

        public Guid RecipientUserId { get; private set; }

        public NotificationStatus Status { get; private set; }

        public int AttemptCount { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

        public DateTime? ProcessingStartedAtUtc { get; private set; }

        public byte[] RowVersion { get; private set; } = null!;

        private NotificationOutbox()
        {
        }

        private NotificationOutbox(
            Guid ringEventId,
            Guid recipientUserId)
        {
            if (ringEventId == Guid.Empty)
                throw new DomainException("RingEventId cannot be empty.");

            if (recipientUserId == Guid.Empty)
                throw new DomainException("RecipientUserId cannot be empty.");

            Id = Guid.NewGuid();
            RingEventId = ringEventId;
            RecipientUserId = recipientUserId;
            Status = NotificationStatus.Pending;
            AttemptCount = 0;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public static NotificationOutbox Create(
            Guid ringEventId,
            Guid recipientUserId)
        {
            return new NotificationOutbox(
                ringEventId,
                recipientUserId);
        }

        public void MarkProcessing()
        {
            if (Status == NotificationStatus.Sent)
            {
                throw new DomainException($"Cannot process notification '{Id}' because it has already been sent.");
            }

            Status = NotificationStatus.Processing;
            AttemptCount++;
            ProcessingStartedAtUtc = DateTime.UtcNow;
        }

        public void MarkSent()
        {
            if (Status != NotificationStatus.Processing)
            {
                throw new DomainException($"Cannot mark notification '{Id}' as sent because it is in status {Status}.");
            }

            Status = NotificationStatus.Sent;
            ProcessingStartedAtUtc = null;
        }

        public void MarkFailed()
        {
            if (Status == NotificationStatus.Sent)
            {
                throw new DomainException($"Cannot mark notification '{Id}' as failed because it has already been sent.");
            }

            if (Status != NotificationStatus.Processing)
            {
                throw new DomainException($"Cannot mark notification '{Id}' as failed because it is not currently processing.");
            }

            Status = NotificationStatus.Failed;
            ProcessingStartedAtUtc = null;
        }

        public bool IsStrandedProcessing(DateTime utcNow, TimeSpan processingTimeout)
        {
            return Status == NotificationStatus.Processing
                && (ProcessingStartedAtUtc is null
                    || ProcessingStartedAtUtc.Value < utcNow.Subtract(processingTimeout));
        }
    }
}
