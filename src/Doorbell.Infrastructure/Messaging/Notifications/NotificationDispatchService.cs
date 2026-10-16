using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class NotificationDispatchService : INotificationDispatchService
{
    private readonly IUserConnectionRegistry _connections;
    private readonly ISignalRNotificationSender _signalR;
    private readonly IFcmNotificationSender _fcm;
    private readonly NotificationOutboxOptions _options;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        IUserConnectionRegistry connections,
        ISignalRNotificationSender signalR,
        IFcmNotificationSender fcm,
        IOptions<NotificationOutboxOptions> options,
        ILogger<NotificationDispatchService> logger)
    {
        _connections = connections;
        _signalR = signalR;
        _fcm = fcm;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<NotificationDispatchResult> DispatchAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        var userId = message.RecipientUserId;

        if (_connections.IsConnected(userId))
        {
            var signalResult = await _signalR.SendAsync(
                userId,
                message,
                cancellationToken);

            if (signalResult.IsSuccess)
            {
                return new NotificationDispatchResult(
                    IsSuccess: true,
                    Channel: NotificationChannel.SignalR,
                    UsedFallback: false,
                    ErrorMessage: null);
            }

            _logger.LogWarning(
                "SignalR delivery failed for recipient {UserId}: {Error}. Falling back to FCM.",
                userId,
                signalResult.ErrorMessage);

            if (!_options.FallbackToFcm)
            {
                return new NotificationDispatchResult(
                    IsSuccess: false,
                    Channel: NotificationChannel.SignalR,
                    UsedFallback: false,
                    ErrorMessage: signalResult.ErrorMessage);
            }

            return await SendViaFcmAsync(
                message,
                signalResult.ErrorMessage,
                cancellationToken);
        }

        _logger.LogInformation(
            "Recipient {UserId} has no active SignalR connection; delivering via FCM.",
            userId);

        return await SendViaFcmAsync(
            message,
            null,
            cancellationToken);
    }

    private async Task<NotificationDispatchResult> SendViaFcmAsync(
        NotificationMessage message,
        string? priorError,
        CancellationToken cancellationToken)
    {
        var result = await _fcm.SendAsync(
            message.RecipientUserId,
            message,
            cancellationToken);

        return new NotificationDispatchResult(
            IsSuccess: result.IsSuccess,
            Channel: NotificationChannel.Fcm,
            UsedFallback: true,
            ErrorMessage: result.ErrorMessage ?? priorError);
    }
}
