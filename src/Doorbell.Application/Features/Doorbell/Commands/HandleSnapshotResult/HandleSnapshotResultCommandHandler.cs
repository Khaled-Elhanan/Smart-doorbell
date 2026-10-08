using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Application.Features.Doorbell.Commands.HandleSnapshotResult;

public sealed class HandleSnapshotResultCommandHandler
    : IRequestHandler<HandleSnapshotResultCommand>
{
    private readonly IDeviceCommandRepository _deviceCommandRepository;
    private readonly IDoorbellRingEventRepository _ringEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HandleSnapshotResultCommandHandler> _logger;

    public HandleSnapshotResultCommandHandler(
        IDeviceCommandRepository deviceCommandRepository,
        IDoorbellRingEventRepository ringEventRepository,
        IUnitOfWork unitOfWork,
        ILogger<HandleSnapshotResultCommandHandler> logger)
    {
        _deviceCommandRepository = deviceCommandRepository;
        _ringEventRepository = ringEventRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(
        HandleSnapshotResultCommand request,
        CancellationToken cancellationToken)
    {
        var command = await _deviceCommandRepository.GetByRequestIdAsync(
            request.RequestId,
            cancellationToken);

        if (command is null)
        {
            _logger.LogWarning(
                "Snapshot result references unknown RequestId {RequestId}; ignored.",
                request.RequestId);

            return;
        }

        if (command.CommandType != DeviceCommandType.Snapshot)
        {
            _logger.LogWarning(
                "Snapshot result references non-snapshot command {RequestId}; ignored.",
                request.RequestId);

            return;
        }

        var ringEvent = await _ringEventRepository.GetByIdAsync(
            command.RingEventId,
            cancellationToken);

        if (ringEvent is null)
        {
            throw new InvalidOperationException(
                $"Ring event '{command.RingEventId}' was not found.");
        }

        if (ringEvent.SnapshotStatus == SnapshotStatus.Captured)
        {
            _logger.LogInformation(
                "Snapshot for ring event {RingEventId} was already captured; duplicate result ignored.",
                ringEvent.Id);

            return;
        }

        if (string.IsNullOrWhiteSpace(command.StorageKey))
        {
            throw new InvalidOperationException(
                $"Snapshot command '{request.RequestId}' has no storage key.");
        }

        ringEvent.ApplySnapshotCaptured(
            command.StorageKey,
            request.Resolution,
            request.CapturedAtUtc);

        if (command.Status == DeviceCommandStatus.Sent)
        {
            command.MarkCompleted();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Snapshot captured for ring event {RingEventId}. StorageKey: {StorageKey}, Resolution: {Resolution}",
            ringEvent.Id,
            command.StorageKey,
            request.Resolution);
    }
}
