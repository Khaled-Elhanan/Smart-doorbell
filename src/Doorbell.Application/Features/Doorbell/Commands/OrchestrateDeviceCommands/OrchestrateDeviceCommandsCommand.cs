using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Doorbell.Commands.OrchestrateDeviceCommands
{
    public sealed record OrchestrateDeviceCommandsCommand(
     Guid RingEventId) : IRequest;
}
