using System.Security.Claims;
using Doorbell.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IUserConnectionRegistry _registry;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        IUserConnectionRegistry registry,
        IUserRepository userRepository,
        ILogger<NotificationHub> logger)
    {
        _registry = registry;
        _userRepository = userRepository;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = await ResolveUserIdAsync();

        if (userId is null)
        {
            _logger.LogWarning(
                "Rejected SignalR connection {ConnectionId}: no known user for the authenticated subject.",
                Context.ConnectionId);

            Context.Abort();
            return;
        }

        _registry.Add(userId.Value, Context.ConnectionId);

        _logger.LogInformation(
            "SignalR connection {ConnectionId} registered for user {UserId}.",
            Context.ConnectionId,
            userId.Value);

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _registry.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private async Task<Guid?> ResolveUserIdAsync()
    {
        var principal = Context.User;

        var subject = principal?.FindFirst("sub")?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var user = await _userRepository.GetBySubjectIdAsync(
            subject,
            Context.ConnectionAborted);

        return user?.Id;
    }
}
