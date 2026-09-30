using Doorbell.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Abstractions.Persistence
{
    public interface IDoorbellRingEventRepository
    {
        Task<DoorbellRingEvent?> GetByDeviceEventIdAsync(
            Guid deviceId,
            string deviceEventId,
            CancellationToken cancellationToken);

        Task AddAsync(
            DoorbellRingEvent ringEvent,
            CancellationToken cancellationToken);
    }
}
