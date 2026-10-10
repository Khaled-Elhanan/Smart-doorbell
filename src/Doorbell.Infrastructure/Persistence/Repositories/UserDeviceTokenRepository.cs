using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Doorbell.Infrastructure.Persistence.Repositories
{
    public sealed class UserDeviceTokenRepository : IUserDeviceTokenRepository
    {
        private readonly ApplicationDbContext _context;

        public UserDeviceTokenRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(
            UserDeviceToken token,
            CancellationToken cancellationToken)
        {
            var existing = await _context.UserDeviceTokens
                .FirstOrDefaultAsync(
                    t => t.UserId == token.UserId && t.Token == token.Token,
                    cancellationToken);

            if (existing is not null)
            {
                existing.Touch();
                return;
            }

            await _context.UserDeviceTokens.AddAsync(token, cancellationToken);
        }

        public async Task<bool> RemoveAsync(
            Guid userId,
            string token,
            CancellationToken cancellationToken)
        {
            var existing = await _context.UserDeviceTokens
                .FirstOrDefaultAsync(
                    t => t.UserId == userId && t.Token == token,
                    cancellationToken);

            if (existing is null)
            {
                return false;
            }

            _context.UserDeviceTokens.Remove(existing);
            return true;
        }

        public async Task<IReadOnlyList<UserDeviceToken>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return await _context.UserDeviceTokens
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }
    }
}
