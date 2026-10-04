using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Features.Doorbell.Commands.ReceiveRing
{
    public sealed record ReceiveRingResult(
    Guid RingEventId,
    bool IsNew);
}
