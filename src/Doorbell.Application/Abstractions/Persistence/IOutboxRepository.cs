using Doorbell.Domain.Entities;

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

        Task<NotificationOutbox?> GetByRingEventIdAsync(
            Guid ringEventId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<NotificationOutbox>> GetUnprocessedAsync(
            int maxBatchSize,
            int maxAttempts,
            TimeSpan processingTimeout,
            CancellationToken cancellationToken);

        Task AddAttemptAsync(
            NotificationAttempt attempt,
            CancellationToken cancellationToken);
    }
}