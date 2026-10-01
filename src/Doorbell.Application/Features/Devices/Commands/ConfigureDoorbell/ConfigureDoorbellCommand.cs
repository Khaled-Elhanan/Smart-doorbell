using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Devices.Commands.ConfigureDoorbell
{
    public sealed record ConfigureDoorbellCommand(
     Guid DeviceId,
     string RingButtonEntityCode,
     string CameraEntityCode,
     string ChimeEntityCode,
     string DefaultChimeMelodyId,
     int SnapshotTimeoutSeconds) : IRequest<Guid>;
}
