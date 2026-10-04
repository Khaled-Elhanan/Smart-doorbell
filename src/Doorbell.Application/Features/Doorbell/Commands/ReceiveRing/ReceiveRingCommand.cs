using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Doorbell.Commands.ReceiveRing
{
    public sealed record ReceiveRingCommand(
    Guid DeviceId,
    string DeviceEventId,
    DateTime RingTimestampUtc) : IRequest<ReceiveRingResult>;
}
