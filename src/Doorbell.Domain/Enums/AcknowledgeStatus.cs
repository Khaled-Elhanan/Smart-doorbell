using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Enums
{
    public enum AcknowledgeStatus
    {
        Pending = 0,
        Acknowledged = 1,
        Missed = 2
    }
}
