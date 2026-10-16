 using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Features.Notifications.Commands.ProcessNotification;
using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Messaging.Notifications;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Doorbell.Infrastructure.BackgroundJobs;

public sealed class NotificationOutboxProcessorJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationOutboxOptions _options;
    private readonly ILogger<NotificationOutboxProcessorJob> _logger;

    public NotificationOutboxProcessorJob(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationOutboxOptions> options,
        ILogger<NotificationOutboxProcessorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the notification outbox.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<NotificationOutbox> candidates;

        using (var scope = _scopeFactory.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

            candidates = await repository.GetUnprocessedAsync(
                _options.BatchSize,
                _options.MaxAttempts,
                TimeSpan.FromSeconds(Math.Max(0, _options.ProcessingTimeoutSeconds)),
                stoppingToken);
        }

        if (candidates.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} notification outbox candidate(s).", candidates.Count);

        foreach (var candidate in candidates)
        {
            stoppingToken.ThrowIfCancellationRequested();

            using var scope = _scopeFactory.CreateScope();

            try
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                await sender.Send(
                    new ProcessNotificationCommand(candidate.Id),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox record {OutboxId}.", candidate.Id);
            }
        }
    }
}
