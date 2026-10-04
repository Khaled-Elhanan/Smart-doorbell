using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;

namespace Doorbell.Application.Abstractions.Repositories;

public interface IDeviceCommandRepository
{
    Task<DeviceCommand?> GetByRequestIdAsync(
        string requestId,
        CancellationToken cancellationToken);

    Task<DeviceCommand?> GetByRingEventIdAsync(
        Guid ringEventId,
        DeviceCommandType commandType,
        CancellationToken cancellationToken);

    Task AddAsync(
        DeviceCommand deviceCommand,
        CancellationToken cancellationToken);
}