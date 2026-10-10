using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class MockNotificationDispatchService : INotificationDispatchService
{
    private readonly ILogger<MockNotificationDispatchService> _logger;

    public MockNotificationDispatchService(ILogger<MockNotificationDispatchService> logger)
    {
        _logger = logger;
    }

    public async Task<NotificationDispatchResult> DispatchAsync(
        Guid recipientUserId,
        Guid ringEventId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Mock dispatching notification to user {RecipientUserId} for ring {RingEventId}.",
            recipientUserId,
            ringEventId);

        // Simulate network delay
        await Task.Delay(50, cancellationToken);

        return new NotificationDispatchResult(true, null);
    }
}
