using System;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Domain.Exceptions;
using Xunit;

namespace Doorbell.Tests.Entities;

public class NotificationOutboxDeliveryTests
{
    [Fact]
    public void MarkProcessing_RecordsProcessingStart()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        var before = DateTime.UtcNow.AddSeconds(-1);
        outbox.MarkProcessing();

        Assert.Equal(NotificationStatus.Processing, outbox.Status);
        Assert.NotNull(outbox.ProcessingStartedAtUtc);
        Assert.True(outbox.ProcessingStartedAtUtc >= before);
        Assert.Equal(1, outbox.AttemptCount);
    }

    [Fact]
    public void MarkSent_ClearsProcessingStart()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();

        outbox.MarkSent();

        Assert.Equal(NotificationStatus.Sent, outbox.Status);
        Assert.Null(outbox.ProcessingStartedAtUtc);
    }

    [Fact]
    public void MarkFailed_ClearsProcessingStart()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();

        outbox.MarkFailed();

        Assert.Equal(NotificationStatus.Failed, outbox.Status);
        Assert.Null(outbox.ProcessingStartedAtUtc);
    }

    [Fact]
    public void MarkProcessing_FromSent_Throws()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();
        outbox.MarkSent();

        Assert.Throws<DomainException>(() => outbox.MarkProcessing());
    }

    [Fact]
    public void MarkFailed_FromPending_Throws()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => outbox.MarkFailed());
    }

    [Fact]
    public void MarkSent_FromPending_Throws()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<DomainException>(() => outbox.MarkSent());
    }

    [Fact]
    public void IsStrandedProcessing_TrueWhenProcessingLongerThanTimeout()
    {
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        outbox.MarkProcessing();
        Assert.False(outbox.IsStrandedProcessing(DateTime.UtcNow, TimeSpan.FromMinutes(5)));
        Assert.True(outbox.IsStrandedProcessing(
            DateTime.UtcNow.AddMinutes(10),
            TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void NotificationAttempt_RecordsChannelAndFallback()
    {
        var attempt = NotificationAttempt.Create(
            Guid.NewGuid(),
            1,
            succeeded: true,
            errorMessage: null,
            channel: NotificationChannel.Fcm,
            usedFallback: true);

        Assert.Equal(NotificationChannel.Fcm, attempt.Channel);
        Assert.True(attempt.UsedFallback);
    }
}
