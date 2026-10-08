using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Application.Features.Doorbell.Commands.ReconcileChimeTimeouts;
using Doorbell.Application.Features.Doorbell.Commands.ReconcileSnapshotTimeouts;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doorbell.Infrastructure.BackgroundJobs;

public sealed class DeviceCommandTimeoutReconciler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _interval;
    private readonly int _chimeTimeoutSeconds;
    private readonly ILogger<DeviceCommandTimeoutReconciler> _logger;

    public DeviceCommandTimeoutReconciler(
        IServiceScopeFactory scopeFactory,
        IOptions<DeviceCommandReconcilerOptions> options,
        ILogger<DeviceCommandTimeoutReconciler> logger)
    {
        _scopeFactory = scopeFactory;
        _interval = TimeSpan.FromSeconds(
            Math.Max(1, options.Value.IntervalSeconds));
        _chimeTimeoutSeconds = Math.Max(
            1,
            options.Value.ChimeTimeoutSeconds);
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
                    "Device command timeout reconciliation failed.");
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

        var snapshotCandidates =
            await repository.GetSnapshotTimeoutCandidatesAsync(stoppingToken);

        var chimeCandidates =
            await repository.GetChimeTimeoutCandidatesAsync(
                _chimeTimeoutSeconds,
                stoppingToken);

        var now = DateTime.UtcNow;

        foreach (var candidate in snapshotCandidates)
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

        foreach (var candidate in chimeCandidates)
        {
            if ((now - candidate.SentAtUtc).TotalSeconds <
                candidate.TimeoutSeconds)
            {
                continue;
            }

            try
            {
                await sender.Send(
                    new TimeOutChimeCommand(candidate),
                    stoppingToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // A chime ack or an earlier pass already changed this row.
                _logger.LogWarning(
                    ex,
                    "Concurrent update while timing out chime for ring event {RingEventId}; skipped.",
                    candidate.RingEventId);
            }
        }
    }
}