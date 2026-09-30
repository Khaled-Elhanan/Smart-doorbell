using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Abstractions.Persistence
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(
            CancellationToken cancellationToken);
    }
}
