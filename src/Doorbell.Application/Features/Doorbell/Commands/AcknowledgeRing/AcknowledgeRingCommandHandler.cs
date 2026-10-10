using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Abstractions.Security;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.AcknowledgeRing;

public sealed class AcknowledgeRingCommandHandler
    : IRequestHandler<AcknowledgeRingCommand, AcknowledgeRingResult>
{
    private readonly IDoorbellRingEventRepository _ringEventRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public AcknowledgeRingCommandHandler(
        IDoorbellRingEventRepository ringEventRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _ringEventRepository = ringEventRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<AcknowledgeRingResult> Handle(
        AcknowledgeRingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException(
                "Authentication is required to acknowledge a ring event.");
        }

        var currentUserId = await _currentUser.GetUserIdAsync(cancellationToken);

        if (currentUserId is null)
        {
            throw new UnauthorizedException(
                "The authenticated identity is not associated with a known user.");
        }

        var ringEvent = await _ringEventRepository.GetByIdAsync(
            request.RingEventId,
            cancellationToken);

        if (ringEvent is null)
        {
            throw new NotFoundException(
                $"Ring event '{request.RingEventId}' was not found.");
        }

        var device = await _deviceRepository.GetByIdAsync(
            ringEvent.DeviceId,
            cancellationToken);

        if (device is null)
        {
            throw new NotFoundException(
                $"Device '{ringEvent.DeviceId}' was not found.");
        }

        if (device.OwnerUserId != currentUserId.Value)
        {
            throw new ForbiddenException(
                "You are not authorized to acknowledge this ring event.");
        }
        if (ringEvent.AcknowledgeStatus == AcknowledgeStatus.Acknowledged)
        {
            return new AcknowledgeRingResult(
                ringEvent.Id,
                WasAcknowledged: true,
                AlreadyAcknowledged: true,
                AcknowledgedAtUtc: ringEvent.AcknowledgedAtUtc);
        }

        ringEvent.Acknowledge();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcknowledgeRingResult(
            ringEvent.Id,
            WasAcknowledged: true,
            AlreadyAcknowledged: false,
            AcknowledgedAtUtc: ringEvent.AcknowledgedAtUtc);
    }
}
