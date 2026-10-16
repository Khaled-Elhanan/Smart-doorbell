using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;

namespace Doorbell.Application.Abstractions.Persistence
{
    public interface IOutboxRepository
    {
        Task AddAsync(
            NotificationOutbox outbox,
            CancellationToken cancellationToken);

        Task<NotificationOutbox?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<NotificationOutbox>> GetByRingEventIdAsync(
            Guid ringEventId,
            CancellationToken cancellationToken);

        Task<NotificationOutbox?> GetByRingEventIdAndKindAsync(
            Guid ringEventId,
            NotificationKind kind,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<NotificationOutbox>> GetUnprocessedAsync(
            int maxBatchSize,
            int maxAttempts,
            TimeSpan processingTimeout,
            CancellationToken cancellationToken);

        Task AddAttemptAsync(
            NotificationAttempt attempt,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<NotificationAttempt>> GetAttemptsAsync(
            Guid outboxId,
            CancellationToken cancellationToken);
    }
}
