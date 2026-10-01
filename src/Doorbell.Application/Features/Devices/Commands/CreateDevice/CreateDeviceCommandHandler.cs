using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Devices.Commands.CreateDevice
{
    public sealed class CreateDeviceCommandHandler
    : IRequestHandler<CreateDeviceCommand, Guid>
    {
        private readonly IUserRepository _userRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateDeviceCommandHandler(
            IUserRepository userRepository,
            IDeviceRepository deviceRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _deviceRepository = deviceRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(
            CreateDeviceCommand request,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(
                request.OwnerUserId,
                cancellationToken);

            if (user is null)
            {
                throw new InvalidOperationException(
                    "Owner user was not found.");
            }

            var device = new Device(
                request.OwnerUserId,
                request.DeviceType);

            await _deviceRepository.AddAsync(
                device,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return device.Id;
        }
    }
}
