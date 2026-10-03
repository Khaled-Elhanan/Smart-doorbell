using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Domain.Enums
{

    public enum DeviceCommandStatus
    {
        Pending = 0,
        Sent = 1,
        Completed = 2,
        Failed = 3
    }
}
