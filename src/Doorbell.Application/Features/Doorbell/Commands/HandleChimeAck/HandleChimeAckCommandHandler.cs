using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Application.Features.Doorbell.Commands.HandleChimeAck;

public sealed class HandleChimeAckCommandHandler
    : IRequestHandler<HandleChimeAckCommand>
{
    private readonly IDeviceCommandRepository _deviceCommandRepository;
    private readonly IDoorbellRingEventRepository _ringEventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<HandleChimeAckCommandHandler> _logger;

    public HandleChimeAckCommandHandler(
        IDeviceCommandRepository deviceCommandRepository,
        IDoorbellRingEventRepository ringEventRepository,
        IUnitOfWork unitOfWork,
        ILogger<HandleChimeAckCommandHandler> logger)
    {
        _deviceCommandRepository = deviceCommandRepository;
        _ringEventRepository = ringEventRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(
        HandleChimeAckCommand request,
        CancellationToken cancellationToken)
    {
        var command = await _deviceCommandRepository.GetByRequestIdAsync(
            request.RequestId,
            cancellationToken);

        if (command is null)
        {
            _logger.LogWarning(
                "Chime ack references unknown RequestId {RequestId}; ignored.",
                request.RequestId);

            return;
        }

        if (command.CommandType != DeviceCommandType.Chime)
        {
            _logger.LogWarning(
                "Chime ack references non-chime command {RequestId}; ignored.",
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

        if (ringEvent.ChimeStatus == ChimeStatus.Played)
        {
            _logger.LogInformation(
                "Chime for ring event {RingEventId} was already played; duplicate ack ignored.",
                ringEvent.Id);

            return;
        }

        if (ringEvent.ChimeStatus == ChimeStatus.NotRequested)
        {
            _logger.LogWarning(
                "Chime for ring event {RingEventId} was never requested; ack ignored.",
                ringEvent.Id);

            return;
        }

        ringEvent.MarkChimePlayed();

        if (command.Status == DeviceCommandStatus.Sent)
        {
            command.MarkCompleted();
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Chime played for ring event {RingEventId}. RequestId: {RequestId}",
            ringEvent.Id,
            request.RequestId);
    }
}