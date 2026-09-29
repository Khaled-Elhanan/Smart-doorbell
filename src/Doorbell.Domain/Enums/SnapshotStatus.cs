using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Enums
{
    public enum SnapshotStatus
    {
        NotRequested = 0,
        Requested = 1,
        Captured = 2,
        TimedOut = 3,
        Failed = 4
    }
}
