using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Devices.Commands.CreateDevice
{
    public sealed record CreateDeviceCommand(
    Guid OwnerUserId,
    string DeviceType) : IRequest<Guid>;
}
