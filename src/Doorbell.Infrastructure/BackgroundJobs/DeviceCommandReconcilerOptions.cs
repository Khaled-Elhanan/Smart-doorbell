namespace Doorbell.Infrastructure.BackgroundJobs;

public sealed class DeviceCommandReconcilerOptions
{
    public int IntervalSeconds { get; set; } = 5;

    public int ChimeTimeoutSeconds { get; set; } = 30;
}