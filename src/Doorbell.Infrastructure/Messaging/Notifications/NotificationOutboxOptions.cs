namespace Doorbell.Infrastructure.Messaging.Notifications;

public sealed class NotificationOutboxOptions
{
    public const string SectionName = "NotificationOutbox";
    public int PollingIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 25;
    public int MaxAttempts { get; set; } = 3;

    public int ProcessingTimeoutSeconds { get; set; } = 300;

    public bool FallbackToFcm { get; set; } = true;
}
