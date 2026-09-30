using Doorbell.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Abstractions.Persistence
{
    public interface IDoorbellCompositionRepository
    {
        Task<AdvancedDoorbellComposition?> GetByDeviceIdAsync(
            Guid deviceId,
            CancellationToken cancellationToken);
    }
}
