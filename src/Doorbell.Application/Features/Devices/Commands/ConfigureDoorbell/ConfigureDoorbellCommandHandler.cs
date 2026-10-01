using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Devices.Commands.ConfigureDoorbell
{
    public sealed class ConfigureDoorbellCommandHandler
    : IRequestHandler<ConfigureDoorbellCommand, Guid>
    {
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDoorbellCompositionRepository _compositionRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ConfigureDoorbellCommandHandler(
            IDeviceRepository deviceRepository,
            IDoorbellCompositionRepository compositionRepository,
            IUnitOfWork unitOfWork)
        {
            _deviceRepository = deviceRepository;
            _compositionRepository = compositionRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(
            ConfigureDoorbellCommand request,
            CancellationToken cancellationToken)
        {
            var device = await _deviceRepository.GetByIdAsync(
                request.DeviceId,
                cancellationToken);

            if (device is null)
            {
                throw new InvalidOperationException(
                    "Device was not found.");
            }

            var existingComposition =
                await _compositionRepository.GetByDeviceIdAsync(
                    request.DeviceId,
                    cancellationToken);

            if (existingComposition is not null)
            {
                throw new InvalidOperationException(
                    "Doorbell is already configured.");
            }

            var composition = new AdvancedDoorbellComposition(
                request.DeviceId,
                request.RingButtonEntityCode,
                request.CameraEntityCode,
                request.ChimeEntityCode,
                request.DefaultChimeMelodyId,
                request.SnapshotTimeoutSeconds);

            await _compositionRepository.AddAsync(
                composition,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return composition.Id;
        }
    }
}
