using Doorbell.Application.Abstractions.Messaging;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed record NotificationPayload(
    Guid NotificationId,
    Guid RingEventId,
    Guid DeviceId,
    DateTime RingTimestampUtc,
    string? SnapshotStorageKey,
    string? SnapshotResolution)
{
    public static NotificationPayload From(NotificationMessage message)
    {
        return new NotificationPayload(
            message.NotificationId,
            message.RingEventId,
            message.DeviceId,
            message.RingTimestampUtc,
            message.SnapshotStorageKey,
            message.SnapshotResolution);
    }
}

public sealed record NotificationSendResult(
    bool IsSuccess,
    bool IsTransient,
    string? ErrorMessage);
