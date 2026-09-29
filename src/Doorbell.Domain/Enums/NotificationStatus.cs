using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Enums
{
    public enum NotificationStatus
    {
        Pending = 0,
        Processing = 1,
        Sent = 2,
        Failed = 3
    }
}
