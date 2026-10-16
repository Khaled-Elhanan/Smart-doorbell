using Doorbell.Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class SimulatedFcmNotificationSender : IFcmNotificationSender
{
    private readonly ILogger<SimulatedFcmNotificationSender> _logger;

    public SimulatedFcmNotificationSender(
        ILogger<SimulatedFcmNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task<NotificationSendResult> SendAsync(
        Guid userId,
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Simulated FCM push delivered to recipient {UserId} for ring {RingEventId} (notification {NotificationId}).",
            userId,
            message.RingEventId,
            message.NotificationId);

        return Task.FromResult(
            new NotificationSendResult(
                IsSuccess: true,
                IsTransient: false,
                ErrorMessage: null));
    }
}
