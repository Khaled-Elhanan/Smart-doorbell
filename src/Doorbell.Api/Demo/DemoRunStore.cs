using System.Collections.Concurrent;

namespace Doorbell.Api.Demo;

public enum DemoMode
{
    Auto = 0,
    Interactive = 1
}

public enum DemoAckDecision
{
    Upload = 0,
    Discard = 1
}

public sealed class DemoRun
{
    public required string RunId { get; init; }

    public required Guid DeviceId { get; init; }

    public required DemoMode Mode { get; init; }

    public required DateTime RequestedAtUtc { get; init; }

    public TaskCompletionSource<DemoAckDecision> AckGate { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool AwaitingAck { get; set; }

    public bool Cancelled { get; set; }

    public DateTime? AcknowledgedAtUtc { get; set; }

    public DateTime? RejectedAtUtc { get; set; }

    public Guid? RingEventId { get; set; }

    public string? LastPhase { get; set; }
}

public sealed class DemoRunStore
{
    private const int MaxRuns = 30;

    private readonly ConcurrentDictionary<string, DemoRun> _runs =
        new(StringComparer.Ordinal);

    public DemoRun Create(
        string runId,
        Guid deviceId,
        DemoMode mode)
    {
        var run = new DemoRun
        {
            RunId = runId,
            DeviceId = deviceId,
            Mode = mode,
            RequestedAtUtc = DateTime.UtcNow,
            AwaitingAck = mode == DemoMode.Interactive
        };

        _runs[runId] = run;

        Trim();

        return run;
    }

    public DemoRun? Get(string runId)
    {
        return !string.IsNullOrWhiteSpace(runId) && _runs.TryGetValue(runId, out var run)
            ? run
            : null;
    }

    public IReadOnlyList<DemoRun> Recent(int count)
    {
        return _runs.Values
            .OrderByDescending(run => run.RequestedAtUtc)
            .Take(count)
            .ToList();
    }

    private void Trim()
    {
        if (_runs.Count <= MaxRuns)
        {
            return;
        }

        var stale = _runs.Values
            .OrderBy(run => run.RequestedAtUtc)
            .Take(_runs.Count - MaxRuns)
            .ToArray();

        foreach (var run in stale)
        {
            _runs.TryRemove(run.RunId, out _);
        }
    }
}
