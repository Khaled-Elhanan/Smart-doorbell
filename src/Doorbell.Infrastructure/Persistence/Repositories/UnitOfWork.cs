using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Infrastructure.Persistence.Context;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Persistence.Repositories
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            return _context.SaveChangesAsync(
                cancellationToken);
        }
    }
}
