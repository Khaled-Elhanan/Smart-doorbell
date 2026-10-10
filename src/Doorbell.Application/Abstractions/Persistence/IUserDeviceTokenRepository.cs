using Doorbell.Domain.Entities;

namespace Doorbell.Application.Abstractions.Persistence
{
    public interface IUserDeviceTokenRepository
    {
        Task AddAsync(
            UserDeviceToken token,
            CancellationToken cancellationToken);

        Task<bool> RemoveAsync(
            Guid userId,
            string token,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<UserDeviceToken>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken);
    }
}
