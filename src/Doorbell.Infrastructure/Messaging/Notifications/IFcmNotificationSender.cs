using Doorbell.Application.Abstractions.Messaging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public interface IFcmNotificationSender
{
    bool IsConfigured { get; }
    string ConfigurationStatus { get; }

    Task<NotificationSendResult> SendAsync(
        Guid userId,
        NotificationMessage message,
        CancellationToken cancellationToken);
}
