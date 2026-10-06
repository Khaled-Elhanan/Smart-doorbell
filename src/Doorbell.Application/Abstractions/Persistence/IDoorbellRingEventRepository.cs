using Doorbell.Domain.Entities;

namespace Doorbell.Application.Abstractions.Repositories;

public interface IDoorbellRingEventRepository
{
    Task<DoorbellRingEvent?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<DoorbellRingEvent?> GetByDeviceEventIdAsync(
        Guid deviceId,
        string deviceEventId,
        CancellationToken cancellationToken);

    Task AddAsync(
        DoorbellRingEvent ringEvent,
        CancellationToken cancellationToken);
}