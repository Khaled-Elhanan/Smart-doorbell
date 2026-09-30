using Doorbell.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Entities
{
    public sealed class Device
    {
        public Guid Id { get; private set; }

        public Guid OwnerUserId { get; private set; }

        public string DeviceType { get; private set; } = null!;

        private Device()
        {
        }

        public Device(
            Guid ownerUserId,
            string deviceType)
        {
            if (ownerUserId == Guid.Empty)
                throw new DomainException("OwnerUserId cannot be empty.");

            if (string.IsNullOrWhiteSpace(deviceType))
                throw new DomainException("Device type is required.");

            Id = Guid.NewGuid();
            OwnerUserId = ownerUserId;
            DeviceType = deviceType;
        }
    }
}
