using Doorbell.Domain.Entities;

namespace Doorbell.Application.Abstractions.Persistence
{
    public interface IUserRepository
    {
        Task AddAsync(
            User user,
            CancellationToken cancellationToken);

        Task<User?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);

        Task<User?> GetBySubjectIdAsync(
            string subjectId,
            CancellationToken cancellationToken);
    }
}