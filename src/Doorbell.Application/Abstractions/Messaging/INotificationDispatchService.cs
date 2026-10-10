using System;
using System.Threading;
using System.Threading.Tasks;

namespace Doorbell.Application.Abstractions.Messaging;

public sealed record NotificationDispatchResult(bool IsSuccess, string? ErrorMessage);

public interface INotificationDispatchService
{
    Task<NotificationDispatchResult> DispatchAsync(
        Guid recipientUserId,
        Guid ringEventId,
        CancellationToken cancellationToken);
}
