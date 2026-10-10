using Doorbell.Application.Abstractions.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public interface ISignalRNotificationSender
{
    Task<NotificationSendResult> SendAsync(
        Guid userId,
        NotificationMessage message,
        CancellationToken cancellationToken);
}

public sealed class SignalRNotificationSender : ISignalRNotificationSender
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IUserConnectionRegistry _connections;
    private readonly ILogger<SignalRNotificationSender> _logger;

    public SignalRNotificationSender(
        IHubContext<NotificationHub> hubContext,
        IUserConnectionRegistry connections,
        ILogger<SignalRNotificationSender> logger)
    {
        _hubContext = hubContext;
        _connections = connections;
        _logger = logger;
    }

    public async Task<NotificationSendResult> SendAsync(
        Guid userId,
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        var connectionIds = _connections.GetConnections(userId);

        if (connectionIds.Count == 0)
        {
            return new NotificationSendResult(
                IsSuccess: false,
                IsTransient: true,
                ErrorMessage: $"User '{userId}' has no active SignalR connection.");
        }

        try
        {
            await _hubContext.Clients
                .Clients(connectionIds)
                .SendAsync(
                    "RingEventNotification",
                    NotificationPayload.From(message),
                    cancellationToken);

            return new NotificationSendResult(
                IsSuccess: true,
                IsTransient: false,
                ErrorMessage: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SignalR delivery to user {UserId} across {Count} connection(s) failed.",
                userId,
                connectionIds.Count);

            return new NotificationSendResult(
                IsSuccess: false,
                IsTransient: true,
                ErrorMessage: ex.Message);
        }
    }
}
