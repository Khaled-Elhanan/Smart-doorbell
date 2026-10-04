using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Abstractions.Storage
{
    // where i will store the snapshot 
    public interface IStorageKeyGenerator
    {
        string CreateSnapshotKey(
            Guid deviceId,
            Guid ringEventId);
    }
}
