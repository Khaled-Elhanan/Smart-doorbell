using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Doorbell.Infrastructure.Persistence.Repositories
{
    public sealed class OutboxRepository : IOutboxRepository
    {
        private readonly ApplicationDbContext _context;

        public OutboxRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(
            NotificationOutbox outbox,
            CancellationToken cancellationToken)
        {
            await _context.NotificationOutboxes.AddAsync(
                outbox,
                cancellationToken);
        }

        public Task<NotificationOutbox?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return _context.NotificationOutboxes
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<NotificationOutbox>> GetByRingEventIdAsync(
            Guid ringEventId,
            CancellationToken cancellationToken)
        {
            return await _context.NotificationOutboxes
                .AsNoTracking()
                .Where(x => x.RingEventId == ringEventId)
                .OrderBy(x => x.Kind)
                .ToListAsync(cancellationToken);
        }

        public Task<NotificationOutbox?> GetByRingEventIdAndKindAsync(
            Guid ringEventId,
            NotificationKind kind,
            CancellationToken cancellationToken)
        {
            return _context.NotificationOutboxes
                .FirstOrDefaultAsync(
                    x => x.RingEventId == ringEventId && x.Kind == kind,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<NotificationOutbox>> GetUnprocessedAsync(
            int maxBatchSize,
            int maxAttempts,
            TimeSpan processingTimeout,
            CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.Subtract(processingTimeout);

            var query = _context.NotificationOutboxes
                .Where(o =>
                    o.Status == NotificationStatus.Pending
                    || (o.Status == NotificationStatus.Failed && o.AttemptCount < maxAttempts)
                    || (o.Status == NotificationStatus.Processing
                        && (o.ProcessingStartedAtUtc == null
                            || o.ProcessingStartedAtUtc < cutoff)))
                .OrderBy(o => o.CreatedAtUtc)
                .Take(maxBatchSize);

            return await query.ToListAsync(cancellationToken);
        }

        public async Task AddAttemptAsync(
            NotificationAttempt attempt,
            CancellationToken cancellationToken)
        {
            await _context.NotificationAttempts.AddAsync(
                attempt,
                cancellationToken);
        }

        public async Task<IReadOnlyList<NotificationAttempt>> GetAttemptsAsync(
            Guid outboxId,
            CancellationToken cancellationToken)
        {
            return await _context.NotificationAttempts
                .AsNoTracking()
                .Where(a => a.OutboxId == outboxId)
                .OrderBy(a => a.AttemptNumber)
                .ToListAsync(cancellationToken);
        }
    }
}
