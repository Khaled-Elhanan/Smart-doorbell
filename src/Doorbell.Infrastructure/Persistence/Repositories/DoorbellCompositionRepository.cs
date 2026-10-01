using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Persistence.Repositories
{
    public sealed class DoorbellCompositionRepository
     : IDoorbellCompositionRepository
    {
        private readonly ApplicationDbContext _context;

        public DoorbellCompositionRepository(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(
     AdvancedDoorbellComposition composition,
     CancellationToken cancellationToken)
        {
            await _context.AdvancedDoorbellCompositions.AddAsync(
                composition,
                cancellationToken);
        }

        public async Task<AdvancedDoorbellComposition?> GetByDeviceIdAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            return await _context.AdvancedDoorbellCompositions
                .FirstOrDefaultAsync(
                    x => x.DeviceId == deviceId,
                    cancellationToken);
        }
    }
}