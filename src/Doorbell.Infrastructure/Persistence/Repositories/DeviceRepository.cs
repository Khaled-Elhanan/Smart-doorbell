using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Persistence.Repositories
{
    public sealed class DeviceRepository : IDeviceRepository
    {
        private readonly ApplicationDbContext _context;

        public DeviceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(
            Device device,
            CancellationToken cancellationToken)
        {
            await _context.Devices.AddAsync(
                device,
                cancellationToken);
        }

        public Task<Device?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return _context.Devices
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }
    }
    
}
