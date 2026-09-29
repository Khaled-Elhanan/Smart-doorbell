using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Enums
{
    public enum ChimeStatus
    {
        NotRequested = 0,
        Requested = 1,
        Played = 2,
        Failed = 3,
        Unknown = 4
    }
}
