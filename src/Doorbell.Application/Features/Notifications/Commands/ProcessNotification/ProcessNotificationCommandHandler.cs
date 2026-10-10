using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Application.Features.Notifications.Commands.ProcessNotification;

public sealed class ProcessNotificationCommandHandler : IRequestHandler<ProcessNotificationCommand, bool>
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IDoorbellRingEventRepository _ringEventRepository;
    private readonly INotificationDispatchService _dispatchService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessNotificationCommandHandler> _logger;

    public ProcessNotificationCommandHandler(
        IOutboxRepository outboxRepository,
        IDoorbellRingEventRepository ringEventRepository,
        INotificationDispatchService dispatchService,
        IUnitOfWork unitOfWork,
        ILogger<ProcessNotificationCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
        _ringEventRepository = ringEventRepository;
        _dispatchService = dispatchService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(
        ProcessNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var outbox = await _outboxRepository.GetByIdAsync(request.OutboxId, cancellationToken);

        if (outbox is null)
        {
            _logger.LogWarning("Outbox record {OutboxId} not found.", request.OutboxId);
            return false;
        }

        if (outbox.Status == NotificationStatus.Sent)
        {
            _logger.LogInformation("Outbox record {OutboxId} is already sent.", request.OutboxId);
            return false;
        }

        try
        {
            outbox.MarkProcessing();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _logger.LogInformation("Concurrency conflict while claiming outbox {OutboxId}.", request.OutboxId);
            return false;
        }

        var ringEvent = await _ringEventRepository.GetByIdAsync(
            outbox.RingEventId,
            cancellationToken);

        if (ringEvent is null)
        {
            var missingRingAttempt = NotificationAttempt.Create(
                outbox.Id,
                outbox.AttemptCount,
                succeeded: false,
                errorMessage: $"Ring event '{outbox.RingEventId}' was not found.");

            outbox.MarkFailed();
            await _outboxRepository.AddAttemptAsync(missingRingAttempt, cancellationToken);
            await PersistOutcomeAsync(request.OutboxId, cancellationToken);
            return false;
        }

        var message = new NotificationMessage(
            NotificationId: outbox.Id,
            RingEventId: outbox.RingEventId,
            RecipientUserId: outbox.RecipientUserId,
            DeviceId: ringEvent.DeviceId,
            RingTimestampUtc: ringEvent.RingTimestampUtc,
            SnapshotStorageKey: ringEvent.SnapshotStorageKey,
            SnapshotResolution: ringEvent.SnapshotResolution);

        NotificationDispatchResult dispatchResult;

        try
        {
            dispatchResult = await _dispatchService.DispatchAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled dispatch error for outbox {OutboxId}.", request.OutboxId);
            dispatchResult = new NotificationDispatchResult(
                IsSuccess: false,
                Channel: null,
                UsedFallback: false,
                ErrorMessage: ex.Message);
        }

        outbox = await _outboxRepository.GetByIdAsync(request.OutboxId, cancellationToken);
        if (outbox is null)
        {
            return false;
        }

        var attempt = NotificationAttempt.Create(
            outbox.Id,
            outbox.AttemptCount,
            dispatchResult.IsSuccess,
            dispatchResult.ErrorMessage,
            dispatchResult.Channel,
            dispatchResult.UsedFallback);

        if (dispatchResult.IsSuccess)
        {
            outbox.MarkSent();
            _logger.LogInformation(
                "Sent notification for outbox {OutboxId} via {Channel} (fallback: {Fallback}).",
                request.OutboxId,
                dispatchResult.Channel,
                dispatchResult.UsedFallback);
        }
        else
        {
            outbox.MarkFailed();
            _logger.LogWarning(
                "Failed to send notification for outbox {OutboxId} via {Channel}. Error: {Error}",
                request.OutboxId,
                dispatchResult.Channel,
                dispatchResult.ErrorMessage);
        }

        await _outboxRepository.AddAttemptAsync(attempt, cancellationToken);

        await PersistOutcomeAsync(request.OutboxId, cancellationToken);

        return true;
    }

    private async Task PersistOutcomeAsync(Guid outboxId, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while persisting outcome for outbox {OutboxId}.", outboxId);
        }
    }
}
