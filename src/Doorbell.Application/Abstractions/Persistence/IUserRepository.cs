using Doorbell.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
