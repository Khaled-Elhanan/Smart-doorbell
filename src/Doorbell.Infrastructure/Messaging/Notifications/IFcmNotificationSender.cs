using Doorbell.Application.Abstractions.Messaging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public interface IFcmNotificationSender
{
    Task<NotificationSendResult> SendAsync(
        Guid userId,
        NotificationMessage message,
        CancellationToken cancellationToken);
}
