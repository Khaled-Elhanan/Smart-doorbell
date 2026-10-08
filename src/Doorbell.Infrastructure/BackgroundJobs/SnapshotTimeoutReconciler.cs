using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Features.Doorbell.Commands.ReconcileSnapshotTimeouts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doorbell.Infrastructure.BackgroundJobs;

public sealed class SnapshotTimeoutReconciler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly ILogger<SnapshotTimeoutReconciler> _logger;

    public SnapshotTimeoutReconciler(
        IServiceScopeFactory scopeFactory,
        IOptions<SnapshotReconcilerOptions> options,
        ILogger<SnapshotTimeoutReconciler> logger)
    {
        _scopeFactory = scopeFactory;
        _interval = TimeSpan.FromSeconds(
            Math.Max(1, options.Value.IntervalSeconds));
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    _interval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Snapshot timeout reconciliation failed.");
            }
        }
    }

    private async Task ReconcileAsync(
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var repository =
            scope.ServiceProvider.GetRequiredService<IDeviceCommandRepository>();

        var sender =
            scope.ServiceProvider.GetRequiredService<ISender>();

        var candidates =
            await repository.GetSnapshotTimeoutCandidatesAsync(stoppingToken);

        var now = DateTime.UtcNow;

        foreach (var candidate in candidates)
        {
            if ((now - candidate.SentAtUtc).TotalSeconds <
                candidate.TimeoutSeconds)
            {
                continue;
            }

            try
            {
                await sender.Send(
                    new TimeOutSnapshotCommand(candidate),
                    stoppingToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // A snapshot result or an earlier pass already changed this row.
                _logger.LogWarning(
                    ex,
                    "Concurrent update while timing out snapshot for ring event {RingEventId}; skipped.",
                    candidate.RingEventId);
            }
        }
    }
}
