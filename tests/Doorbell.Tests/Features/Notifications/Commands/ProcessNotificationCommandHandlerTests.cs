using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Repositories;
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
    private readonly Mock<IDoorbellRingEventRepository> _ringRepoMock;
    private readonly Mock<INotificationDispatchService> _dispatchMock;
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly ProcessNotificationCommandHandler _handler;

    public ProcessNotificationCommandHandlerTests()
    {
        _outboxRepoMock = new Mock<IOutboxRepository>();
        _ringRepoMock = new Mock<IDoorbellRingEventRepository>();
        _dispatchMock = new Mock<INotificationDispatchService>();
        _uowMock = new Mock<IUnitOfWork>();
        _handler = new ProcessNotificationCommandHandler(
            _outboxRepoMock.Object,
            _ringRepoMock.Object,
            _dispatchMock.Object,
            _uowMock.Object,
            Mock.Of<ILogger<ProcessNotificationCommandHandler>>());
    }

    private static DoorbellRingEvent CreateRingEvent(Guid deviceId)
    {
        return DoorbellRingEvent.Create(
            deviceId,
            "device-event-1",
            DateTime.UtcNow,
            "melody-1");
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
        _dispatchMock.Verify(d => d.DispatchAsync(
            It.IsAny<NotificationMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
        _dispatchMock.Verify(d => d.DispatchAsync(
            It.IsAny<NotificationMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDispatchSucceeds_MarksSentAndAddsAttempt()
    {
        var deviceId = Guid.NewGuid();
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        var ring = CreateRingEvent(deviceId);

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);
        _ringRepoMock.Setup(r => r.GetByIdAsync(outbox.RingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ring);

        _dispatchMock
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(true, NotificationChannel.SignalR, false, null));

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(NotificationStatus.Sent, outbox.Status);

        _outboxRepoMock.Verify(r => r.AddAttemptAsync(It.Is<NotificationAttempt>(a =>
            a.OutboxId == outbox.Id &&
            a.Succeeded == true &&
            a.AttemptNumber == 1 &&
            a.Channel == NotificationChannel.SignalR), It.IsAny<CancellationToken>()), Times.Once);

        // Unit of work is saved twice (claim, then final state)
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WhenDispatchFails_MarksFailedAndAddsAttempt()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        var ring = CreateRingEvent(Guid.NewGuid());

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);
        _ringRepoMock.Setup(r => r.GetByIdAsync(outbox.RingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ring);

        _dispatchMock
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(false, NotificationChannel.Fcm, true, "Network timeout"));

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(NotificationStatus.Failed, outbox.Status);

        _outboxRepoMock.Verify(r => r.AddAttemptAsync(It.Is<NotificationAttempt>(a =>
            a.OutboxId == outbox.Id &&
            a.Succeeded == false &&
            a.ErrorMessage == "Network timeout" &&
            a.UsedFallback == true &&
            a.AttemptNumber == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRingEventMissing_MarksFailed()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);
        _ringRepoMock.Setup(r => r.GetByIdAsync(outbox.RingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DoorbellRingEvent?)null);

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.False(result);
        Assert.Equal(NotificationStatus.Failed, outbox.Status);
        _dispatchMock.Verify(d => d.DispatchAsync(
            It.IsAny<NotificationMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDispatchThrows_MarksFailedAndRecordsError()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        var ring = CreateRingEvent(Guid.NewGuid());

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);
        _ringRepoMock.Setup(r => r.GetByIdAsync(outbox.RingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ring);

        _dispatchMock
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider exploded"));

        var result = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(NotificationStatus.Failed, outbox.Status);
        _outboxRepoMock.Verify(r => r.AddAttemptAsync(It.Is<NotificationAttempt>(a =>
            a.Succeeded == false && a.ErrorMessage == "provider exploded"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RetryAfterFailure_IncrementsAttemptNumber()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        var ring = CreateRingEvent(Guid.NewGuid());

        _outboxRepoMock.Setup(r => r.GetByIdAsync(outbox.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outbox);
        _ringRepoMock.Setup(r => r.GetByIdAsync(outbox.RingEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ring);

        _dispatchMock
            .Setup(d => d.DispatchAsync(It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(false, null, false, "temporary"));

        await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);
        var second = await _handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.True(second);
        Assert.Equal(2, outbox.AttemptCount);

        _outboxRepoMock.Verify(r => r.AddAttemptAsync(
            It.Is<NotificationAttempt>(a => a.AttemptNumber == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
