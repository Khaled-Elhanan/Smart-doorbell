using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
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

        public Task<NotificationOutbox?> GetByRingEventIdAsync(
            Guid ringEventId,
            CancellationToken cancellationToken)
        {
            return _context.NotificationOutboxes
                .FirstOrDefaultAsync(
                    x => x.RingEventId == ringEventId,
                    cancellationToken);
        }
    }
}