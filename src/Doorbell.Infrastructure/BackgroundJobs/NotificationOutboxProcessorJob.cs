using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Features.Notifications.Commands.ProcessNotification;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Doorbell.Infrastructure.BackgroundJobs;

public sealed class NotificationOutboxProcessorJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationOutboxProcessorJob> _logger;

    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private readonly int _maxBatchSize = 50;
    private readonly int _maxAttempts = 3;
    private readonly TimeSpan _processingTimeout = TimeSpan.FromMinutes(5);

    public NotificationOutboxProcessorJob(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationOutboxProcessorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                _logger.LogError(ex, "An error occurred while processing notification outbox.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var candidates = await repository.GetUnprocessedAsync(
            _maxBatchSize,
            _maxAttempts,
            _processingTimeout,
            stoppingToken);

        if (candidates.Count > 0)
        {
            _logger.LogInformation("Found {Count} notification outbox candidates.", candidates.Count);
        }

        foreach (var candidate in candidates)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await sender.Send(new ProcessNotificationCommand(candidate.Id), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox record {OutboxId}.", candidate.Id);
            }
        }
    }
}
