using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Persistence.Repositories
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(
            User user,
            CancellationToken cancellationToken)
        {
            await _context.Users.AddAsync(
                user,
                cancellationToken);
        }

        public Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            return _context.Users
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }
    }
}
