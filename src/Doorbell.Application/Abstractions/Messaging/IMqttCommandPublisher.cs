using System;
using System.Collections.Generic;
using System.Text;

namespace Doorbell.Application.Abstractions.Messaging
{
    public interface IMqttCommandPublisher
    {
        Task PublishAsync(
            string topic,
            object payload,
            CancellationToken cancellationToken);
    }
}
