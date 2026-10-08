using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Doorbell.Infrastructure.Persistence.Repositories;

public sealed class DeviceCommandRepository : IDeviceCommandRepository
{
    private readonly ApplicationDbContext _context;

    public DeviceCommandRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<DeviceCommand?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return _context.DeviceCommands
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public Task<DeviceCommand?> GetByRequestIdAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        return _context.DeviceCommands
            .SingleOrDefaultAsync(
                x => x.RequestId == requestId,
                cancellationToken);
    }

    public Task<DeviceCommand?> GetByRingEventIdAsync(
        Guid ringEventId,
        DeviceCommandType commandType,
        CancellationToken cancellationToken)
    {
        return _context.DeviceCommands
            .SingleOrDefaultAsync(
                x => x.RingEventId == ringEventId
                     && x.CommandType == commandType,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SnapshotCommandTimeout>>
        GetSnapshotTimeoutCandidatesAsync(
            CancellationToken cancellationToken)
    {
        var rows = await (
            from command in _context.DeviceCommands
            join composition in _context.AdvancedDoorbellCompositions
                on command.DeviceId equals composition.DeviceId
            where command.CommandType == DeviceCommandType.Snapshot
                  && command.Status == DeviceCommandStatus.Sent
                  && command.SentAtUtc != null
            select new
            {
                command.Id,
                command.RingEventId,
                SentAtUtc = command.SentAtUtc!.Value,
                composition.SnapshotTimeoutSeconds
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new SnapshotCommandTimeout(
                row.Id,
                row.RingEventId,
                row.SentAtUtc,
                row.SnapshotTimeoutSeconds))
            .ToList();
    }

    public async Task<IReadOnlyList<ChimeCommandTimeout>>
        GetChimeTimeoutCandidatesAsync(
            int timeoutSeconds,
            CancellationToken cancellationToken)
    {
        var rows = await (
            from command in _context.DeviceCommands
            where command.CommandType == DeviceCommandType.Chime
                  && command.Status == DeviceCommandStatus.Sent
                  && command.SentAtUtc != null
            select new
            {
                command.Id,
                command.RingEventId,
                SentAtUtc = command.SentAtUtc!.Value
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ChimeCommandTimeout(
                row.Id,
                row.RingEventId,
                row.SentAtUtc,
                timeoutSeconds))
            .ToList();
    }

    public async Task AddAsync(
        DeviceCommand deviceCommand,
        CancellationToken cancellationToken)
    {
        await _context.DeviceCommands.AddAsync(
            deviceCommand,
            cancellationToken);
    }
}