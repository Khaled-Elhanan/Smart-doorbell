using Doorbell.Application.Abstractions.Recipients;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class NotificationHub : Hub
{
    private readonly IUserConnectionRegistry _registry;
    private readonly IRecipientProvider _recipients;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        IUserConnectionRegistry registry,
        IRecipientProvider recipients,
        ILogger<NotificationHub> logger)
    {
        _registry = registry;
        _recipients = recipients;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = await _recipients.GetRecipientUserIdAsync(
            Context.ConnectionAborted);

        _registry.Add(userId, Context.ConnectionId);

        _logger.LogInformation(
            "SignalR connection {ConnectionId} registered for the default recipient {UserId}.",
            Context.ConnectionId,
            userId);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
