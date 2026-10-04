using Doorbell.Application.Abstractions.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Infrastructure.Storage
{
    public sealed class StorageKeyGenerator : IStorageKeyGenerator
    {
        public string CreateSnapshotKey(
            Guid deviceId,
            Guid ringEventId)
        {
            return
                $"doorbells/{deviceId}/rings/{ringEventId}/snapshot.jpg";
        }
    }
}
