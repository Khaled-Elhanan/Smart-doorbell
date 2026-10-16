using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Abstractions.Storage
{
    public interface IStorageKeyGenerator
    {
        string CreateSnapshotKey(
            Guid deviceId,
            Guid ringEventId);
    }
}
