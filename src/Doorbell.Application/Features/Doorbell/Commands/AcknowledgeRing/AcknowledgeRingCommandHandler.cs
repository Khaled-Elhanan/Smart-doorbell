using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.AcknowledgeRing;

public sealed class AcknowledgeRingCommandHandler
    : IRequestHandler<AcknowledgeRingCommand, AcknowledgeRingResult>
{
    private readonly IDoorbellRingEventRepository _ringEventRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AcknowledgeRingCommandHandler(
        IDoorbellRingEventRepository ringEventRepository,
        IUnitOfWork unitOfWork)
    {
        _ringEventRepository = ringEventRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AcknowledgeRingResult> Handle(
        AcknowledgeRingCommand request,
        CancellationToken cancellationToken)
    {
        var ringEvent = await _ringEventRepository.GetByIdAsync(
            request.RingEventId,
            cancellationToken);

        if (ringEvent is null)
        {
            throw new NotFoundException(
                $"Ring event '{request.RingEventId}' was not found.");
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
