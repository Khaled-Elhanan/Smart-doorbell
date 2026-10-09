using Doorbell.Domain.Exceptions;

namespace Doorbell.Domain.Entities
{
    public sealed class NotificationAttempt
    {
        public Guid Id { get; private set; }

        public Guid OutboxId { get; private set; }

        public int AttemptNumber { get; private set; }

        public DateTime AttemptedAtUtc { get; private set; }

        public bool Succeeded { get; private set; }

        public string? ErrorMessage { get; private set; }

        private NotificationAttempt()
        {
        }

        private NotificationAttempt(
            Guid outboxId,
            int attemptNumber,
            bool succeeded,
            string? errorMessage)
        {
            if (outboxId == Guid.Empty)
                throw new DomainException("OutboxId cannot be empty.");

            if (attemptNumber <= 0)
                throw new DomainException(
                    "Attempt number must be greater than zero.");

            Id = Guid.NewGuid();
            OutboxId = outboxId;
            AttemptNumber = attemptNumber;
            AttemptedAtUtc = DateTime.UtcNow;
            Succeeded = succeeded;
            ErrorMessage = errorMessage;
        }

        public static NotificationAttempt Create(
            Guid outboxId,
            int attemptNumber,
            bool succeeded,
            string? errorMessage = null)
        {
            return new NotificationAttempt(
                outboxId,
                attemptNumber,
                succeeded,
                errorMessage);
        }
    }
}