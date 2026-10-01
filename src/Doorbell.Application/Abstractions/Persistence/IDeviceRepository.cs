using Doorbell.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Abstractions.Persistence
{
    public interface IDeviceRepository
    {
        Task AddAsync(
            Device device,
            CancellationToken cancellationToken);

        Task<Device?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken);
    }
}
