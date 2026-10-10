using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Features.Notifications.Commands.ProcessNotification;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Doorbell.Tests.Features.Notifications.Commands;

public class ProcessNotificationCommandHandlerTests
{
    private readonly Mock<IOutboxRepository> _outboxRepoMock;
    private readonly Mock<INotificationDispatchService> _dispatchMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly ProcessNotificationCommandHandler _handler;

    public ProcessNotificationCommandHandlerTests()
    {
        _outboxRepoMock = new Mock<IOutboxRepository>();
        _dispatchMock = new Mock<INotificationDispatchService>();
        _uowMock = new Mock<IUnitOfWork>();
        _handler = new ProcessNotificationCommandHandler(
            _outboxRepoMock.Object,
            _dispatchMock.Object,
            _uowMock.Object,
            Mock.Of<ILogger<ProcessNotificationCommandHandler>>());
    }

    [Fact]
    public async Task Handle_WhenOutboxNotFound_ReturnsFalse()
    {
        _outboxRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationOutbox?)null);

        var result = await _handler.Handle(new ProcessNotificationCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Handle_WhenAlreadySent_ReturnsFalse()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();
        outbox.MarkSent();

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.False(result);
        _dispatchMock.Verify(d => d.DispatchAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenConcurrencyConflictOnClaim_ReturnsFalse()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);

        _uowMock.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("test"));

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.False(result);
        _dispatchMock.Verify(d => d.DispatchAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDispatchSucceeds_MarksSentAndAddsAttempt()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);

        _dispatchMock.Setup(d => d.DispatchAsync(outbox.RecipientUserId, outbox.RingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(true, null));

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(NotificationStatus.Sent, outbox.Status);
        
        _outboxRepoMock.Verify(r => r.AddAttemptAsync(It.Is<NotificationAttempt>(a => 
            a.OutboxId == outbox.Id && 
            a.Succeeded == true && 
            a.AttemptNumber == 1), It.IsAny<CancellationToken>()), Times.Once);
            
        // Unit of work is saved twice (claim, then final state)
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenDispatchFails_MarksFailedAndAddsAttempt()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);

        _dispatchMock.Setup(d => d.DispatchAsync(outbox.RecipientUserId, outbox.RingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(false, "Network timeout"));

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(NotificationStatus.Failed, outbox.Status);
        
        _outboxRepoMock.Verify(r => r.AddAttemptAsync(It.Is<NotificationAttempt>(a => 
            a.OutboxId == outbox.Id && 
            a.Succeeded == false && 
            a.ErrorMessage == "Network timeout" &&
            a.AttemptNumber == 1), It.IsAny<CancellationToken>()), Times.Once);
    }
}
