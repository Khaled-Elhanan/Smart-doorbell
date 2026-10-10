using System;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using Xunit;

namespace Doorbell.Tests.Entities;

public class NotificationOutboxTests
{
    [Fact]
    public void Create_InitializesCorrectly()
    {
        // Arrange
        var ringEventId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();

        // Act
        var outbox = NotificationOutbox.Create(ringEventId, recipientUserId);

        // Assert
        Assert.Equal(ringEventId, outbox.RingEventId);
        Assert.Equal(recipientUserId, outbox.RecipientUserId);
        Assert.Equal(NotificationStatus.Pending, outbox.Status);
        Assert.Equal(0, outbox.AttemptCount);
    }

    [Fact]
    public void MarkProcessing_FromPending_TransitionsToProcessingAndIncrementsAttemptCount()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        // Act
        outbox.MarkProcessing();

        // Assert
        Assert.Equal(NotificationStatus.Processing, outbox.Status);
        Assert.Equal(1, outbox.AttemptCount);
    }

    [Fact]
    public void MarkProcessing_FromFailed_TransitionsToProcessingAndIncrementsAttemptCount()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing(); // 1
        outbox.MarkFailed();

        // Act
        outbox.MarkProcessing(); // 2

        // Assert
        Assert.Equal(NotificationStatus.Processing, outbox.Status);
        Assert.Equal(2, outbox.AttemptCount);
    }

    [Fact]
    public void MarkProcessing_FromSent_ThrowsDomainException()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();
        outbox.MarkSent();

        // Act & Assert
        Assert.Throws<DomainException>(() => outbox.MarkProcessing());
    }

    [Fact]
    public void MarkSent_FromProcessing_TransitionsToSent()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();

        // Act
        outbox.MarkSent();

        // Assert
        Assert.Equal(NotificationStatus.Sent, outbox.Status);
    }

    [Fact]
    public void MarkSent_FromPending_ThrowsDomainException()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        Assert.Throws<DomainException>(() => outbox.MarkSent());
    }

    [Fact]
    public void MarkFailed_FromProcessing_TransitionsToFailed()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();

        // Act
        outbox.MarkFailed();

        // Assert
        Assert.Equal(NotificationStatus.Failed, outbox.Status);
    }

    [Fact]
    public void MarkFailed_FromPending_ThrowsDomainException()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        Assert.Throws<DomainException>(() => outbox.MarkFailed());
    }

    [Fact]
    public void MarkFailed_FromSent_ThrowsDomainException()
    {
        // Arrange
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();
        outbox.MarkSent();

        // Act & Assert
        Assert.Throws<DomainException>(() => outbox.MarkFailed());
    }
}
