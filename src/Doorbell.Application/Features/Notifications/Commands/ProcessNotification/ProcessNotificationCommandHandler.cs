using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Doorbell.Application.Features.Notifications.Commands.ProcessNotification;

public sealed class ProcessNotificationCommandHandler : IRequestHandler<ProcessNotificationCommand, bool>
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly INotificationDispatchService _dispatchService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessNotificationCommandHandler> _logger;

    public ProcessNotificationCommandHandler(
        IOutboxRepository outboxRepository,
        INotificationDispatchService dispatchService,
        IUnitOfWork unitOfWork,
        ILogger<ProcessNotificationCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
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

        // Claim record under concurrency
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

        
        var dispatchResult = await _dispatchService.DispatchAsync(
            outbox.RecipientUserId,
            outbox.RingEventId,
            cancellationToken);

        // Re-fetch to apply final state
        outbox = await _outboxRepository.GetByIdAsync(request.OutboxId, cancellationToken);
        if (outbox is null) return false;

        var attempt = NotificationAttempt.Create(
            outbox.Id,
            outbox.AttemptCount,
            dispatchResult.IsSuccess,
            dispatchResult.ErrorMessage);

        if (dispatchResult.IsSuccess)
        {
            outbox.MarkSent();
            _logger.LogInformation("Successfully processed outbox {OutboxId}.", request.OutboxId);
        }
        else
        {
            outbox.MarkFailed();
            _logger.LogWarning("Failed to process outbox {OutboxId}. Error: {Error}", request.OutboxId, dispatchResult.ErrorMessage);
        }

        await _outboxRepository.AddAttemptAsync(attempt, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while persisting outcome for outbox {OutboxId}.", request.OutboxId);
            return false;
        }

        return true;
    }
}
