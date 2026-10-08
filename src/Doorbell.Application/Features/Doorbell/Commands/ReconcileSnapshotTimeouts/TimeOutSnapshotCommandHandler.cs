using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Application.Features.Doorbell.Commands.ReconcileSnapshotTimeouts;

public sealed class TimeOutSnapshotCommandHandler
    : IRequestHandler<TimeOutSnapshotCommand>
{
    private readonly IDeviceCommandRepository _deviceCommandRepository;
    private readonly IDoorbellRingEventRepository _ringEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TimeOutSnapshotCommandHandler> _logger;

    public TimeOutSnapshotCommandHandler(
        IDeviceCommandRepository deviceCommandRepository,
        IDoorbellRingEventRepository ringEventRepository,
        IUnitOfWork unitOfWork,
        ILogger<TimeOutSnapshotCommandHandler> logger)
    {
        _deviceCommandRepository = deviceCommandRepository;
        _ringEventRepository = ringEventRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(
        TimeOutSnapshotCommand request,
        CancellationToken cancellationToken)
    {
        var candidate = request.Candidate;

        var command = await _deviceCommandRepository.GetByIdAsync(
            candidate.CommandId,
            cancellationToken);

        if (command is null ||
            command.Status != DeviceCommandStatus.Sent)
        {
            return;
        }

        var ringEvent = await _ringEventRepository.GetByIdAsync(
            candidate.RingEventId,
            cancellationToken);

        if (ringEvent is null ||
            ringEvent.SnapshotStatus != SnapshotStatus.Requested)
        {
            return;
        }

        ringEvent.MarkSnapshotTimedOut();

        command.MarkFailed(
            $"Snapshot timed out after {candidate.TimeoutSeconds}s.");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Snapshot timed out for ring event {RingEventId} after {TimeoutSeconds}s.",
            candidate.RingEventId,
            candidate.TimeoutSeconds);
    }
}
