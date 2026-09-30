using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using MediatR;

namespace Doorbell.Application.Features.Doorbell.Commands.ReceiveRing
{
    public sealed class ReceiveRingCommandHandler
    : IRequestHandler<ReceiveRingCommand, Guid>
    {
        private readonly IDoorbellRingEventRepository _ringEventRepository;
        private readonly IDoorbellCompositionRepository _compositionRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ReceiveRingCommandHandler(
            IDoorbellRingEventRepository ringEventRepository,
            IDoorbellCompositionRepository compositionRepository,
            IUnitOfWork unitOfWork)
        {
            _ringEventRepository = ringEventRepository;
            _compositionRepository = compositionRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(
            ReceiveRingCommand request,
            CancellationToken cancellationToken)
        {
            var existingEvent =
                await _ringEventRepository.GetByDeviceEventIdAsync(
                    request.DeviceId,
                    request.DeviceEventId,
                    cancellationToken);

            if (existingEvent is not null)
                return existingEvent.Id;

            var composition =
                await _compositionRepository.GetByDeviceIdAsync(
                    request.DeviceId,
                    cancellationToken);

            if (composition is null)
            {
                throw new InvalidOperationException(
                    $"No doorbell composition found for device {request.DeviceId}.");
            }

            var ringEvent = DoorbellRingEvent.Create(
                request.DeviceId,
                request.DeviceEventId,
                request.RingTimestampUtc,
                composition.DefaultChimeMelodyId);

            await _ringEventRepository.AddAsync(
                ringEvent,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return ringEvent.Id;
        }
    }

}