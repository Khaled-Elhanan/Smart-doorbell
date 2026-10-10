using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Domain.Enums;

namespace Doorbell.Application.Abstractions.Messaging;

public sealed record NotificationMessage(
    Guid NotificationId,
    Guid RingEventId,
    Guid RecipientUserId,
    Guid DeviceId,
    DateTime RingTimestampUtc,
    string? SnapshotStorageKey,
    string? SnapshotResolution);

public sealed record NotificationDispatchResult(
    bool IsSuccess,
    NotificationChannel? Channel,
    bool UsedFallback,
    string? ErrorMessage);

public interface INotificationDispatchService
{
    Task<NotificationDispatchResult> DispatchAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
