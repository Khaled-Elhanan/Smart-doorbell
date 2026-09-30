using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Doorbell.Infrastructure.Persistence.Repositories
{
    public sealed class DoorbellRingEventRepository
    : IDoorbellRingEventRepository
    {
        private readonly ApplicationDbContext _context;

        public DoorbellRingEventRepository(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DoorbellRingEvent?> GetByDeviceEventIdAsync(
            Guid deviceId,
            string deviceEventId,
            CancellationToken cancellationToken)
        {
            return await _context.DoorbellRingEvents
                .FirstOrDefaultAsync(
                    x => x.DeviceId == deviceId &&
                         x.DeviceEventId == deviceEventId,
                    cancellationToken);
        }

        public async Task AddAsync(
            DoorbellRingEvent ringEvent,
            CancellationToken cancellationToken)
        {
            await _context.DoorbellRingEvents.AddAsync(
                ringEvent,
                cancellationToken);
        }
    }
}
