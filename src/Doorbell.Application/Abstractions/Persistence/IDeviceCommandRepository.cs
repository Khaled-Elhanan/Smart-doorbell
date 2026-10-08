using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;

namespace Doorbell.Application.Abstractions.Repositories;

public interface IDeviceCommandRepository
{
    Task<DeviceCommand?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<DeviceCommand?> GetByRequestIdAsync(
        string requestId,
        CancellationToken cancellationToken);

    Task<DeviceCommand?> GetByRingEventIdAsync(
        Guid ringEventId,
        DeviceCommandType commandType,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SnapshotCommandTimeout>>
        GetSnapshotTimeoutCandidatesAsync(
            CancellationToken cancellationToken);

    Task<IReadOnlyList<ChimeCommandTimeout>>
        GetChimeTimeoutCandidatesAsync(
            int timeoutSeconds,
            CancellationToken cancellationToken);

    Task AddAsync(
        DeviceCommand deviceCommand,
        CancellationToken cancellationToken);
}

public sealed record SnapshotCommandTimeout(
    Guid CommandId,
    Guid RingEventId,
    DateTime SentAtUtc,
    int TimeoutSeconds);

public sealed record ChimeCommandTimeout(
    Guid CommandId,
    Guid RingEventId,
    DateTime SentAtUtc,
    int TimeoutSeconds);
