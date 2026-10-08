namespace Doorbell.Infrastructure.BackgroundJobs;

public sealed class SnapshotReconcilerOptions
{
    public int IntervalSeconds { get; set; } = 5;
}
