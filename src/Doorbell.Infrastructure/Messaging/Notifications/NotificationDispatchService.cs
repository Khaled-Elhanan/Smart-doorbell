using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class NotificationDispatchService : INotificationDispatchService
{
    private readonly IUserConnectionRegistry _connections;
    private readonly ISignalRNotificationSender _signalR;
    private readonly IFcmNotificationSender _fcm;
    private readonly IUserDeviceTokenRepository _tokenRepository;
    private readonly NotificationOutboxOptions _options;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        IUserConnectionRegistry connections,
        ISignalRNotificationSender signalR,
        IFcmNotificationSender fcm,
        IUserDeviceTokenRepository tokenRepository,
        IOptions<NotificationOutboxOptions> options,
        ILogger<NotificationDispatchService> logger)
    {
        _connections = connections;
        _signalR = signalR;
        _fcm = fcm;
        _tokenRepository = tokenRepository;
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
            var signalResult = await _signalR.SendAsync(userId, message, cancellationToken);

            if (signalResult.IsSuccess)
            {
                return new NotificationDispatchResult(
                    IsSuccess: true,
                    Channel: NotificationChannel.SignalR,
                    UsedFallback: false,
                    ErrorMessage: null);
            }

            _logger.LogWarning(
                "SignalR delivery failed for user {UserId}: {Error}",
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

            return await TryFcmAsync(
                message,
                usedFallback: true,
                priorError: signalResult.ErrorMessage,
                cancellationToken);
        }

        return await TryFcmAsync(
            message,
            usedFallback: false,
            priorError: null,
            cancellationToken);
    }

    private async Task<NotificationDispatchResult> TryFcmAsync(
        NotificationMessage message,
        bool usedFallback,
        string? priorError,
        CancellationToken cancellationToken)
    {
        var userId = message.RecipientUserId;

        var tokens = await _tokenRepository.GetByUserIdAsync(userId, cancellationToken);

        if (tokens.Count == 0)
        {
            var detail = priorError is null
                ? "No active SignalR connection and no FCM device token is registered for the recipient."
                : "SignalR delivery failed and no FCM device token is registered for the recipient. "
                    + $"SignalR error: {priorError}";

            return new NotificationDispatchResult(
                IsSuccess: false,
                Channel: priorError is null ? null : NotificationChannel.SignalR,
                UsedFallback: usedFallback,
                ErrorMessage: detail);
        }

        if (!_fcm.IsConfigured)
        {
            return new NotificationDispatchResult(
                IsSuccess: false,
                Channel: NotificationChannel.Fcm,
                UsedFallback: usedFallback,
                ErrorMessage:
                    "FCM delivery is required because no active SignalR connection is available, "
                    + $"but Firebase is not configured: {_fcm.ConfigurationStatus}");
        }

        var fcmResult = await _fcm.SendAsync(userId, message, cancellationToken);

        return new NotificationDispatchResult(
            IsSuccess: fcmResult.IsSuccess,
            Channel: NotificationChannel.Fcm,
            UsedFallback: usedFallback,
            ErrorMessage: fcmResult.ErrorMessage);
    }
}
