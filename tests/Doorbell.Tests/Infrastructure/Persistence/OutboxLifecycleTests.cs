using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Features.Notifications.Commands.ProcessNotification;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Infrastructure.Persistence.Context;
using Doorbell.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Doorbell.Tests.Infrastructure.Persistence;

public class OutboxLifecycleTests
{
    private static ApplicationDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static T WithRowVersion<T>(T entity) where T : class
    {
        var field = typeof(T).GetField(
            "<RowVersion>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        field?.SetValue(entity, new byte[] { 1 });

        return entity;
    }

    private sealed class RecordingDispatchService : INotificationDispatchService
    {
        private readonly bool _success;

        public RecordingDispatchService(bool success)
        {
            _success = success;
        }

        public int Calls { get; private set; }

        public NotificationMessage? LastMessage { get; private set; }

        public Task<NotificationDispatchResult> DispatchAsync(
            NotificationMessage message,
            CancellationToken cancellationToken)
        {
            Calls++;
            LastMessage = message;

            return Task.FromResult(new NotificationDispatchResult(
                _success,
                NotificationChannel.SignalR,
                false,
                _success ? null : "boom"));
        }
    }

    private static ProcessNotificationCommandHandler CreateHandler(
        ApplicationDbContext context,
        INotificationDispatchService dispatch)
    {
        return new ProcessNotificationCommandHandler(
            new OutboxRepository(context),
            new DoorbellRingEventRepository(context),
            dispatch,
            new UnitOfWork(context),
            Mock.Of<ILogger<ProcessNotificationCommandHandler>>());
    }

    [Fact]
    public async Task Process_WhenProviderAccepts_MarksOutboxSent()
    {
        await using var context = CreateContext(nameof(Process_WhenProviderAccepts_MarksOutboxSent));

        var deviceId = Guid.NewGuid();
        var ring = DoorbellRingEvent.Create(deviceId, "evt-1", DateTime.UtcNow, "melody");
        var outbox = NotificationOutbox.Create(ring.Id, Guid.NewGuid());

        await context.DoorbellRingEvents.AddAsync(WithRowVersion(ring));
        await context.NotificationOutboxes.AddAsync(WithRowVersion(outbox));
        await context.SaveChangesAsync();

        var dispatch = new RecordingDispatchService(success: true);
        var handler = CreateHandler(context, dispatch);

        var processed = await handler.Handle(
            new ProcessNotificationCommand(outbox.Id),
            CancellationToken.None);

        Assert.True(processed);
        Assert.Equal(1, dispatch.Calls);
        Assert.Equal(deviceId, dispatch.LastMessage!.DeviceId);

        var persisted = await context.NotificationOutboxes.FindAsync(outbox.Id);
        Assert.Equal(NotificationStatus.Sent, persisted!.Status);
        Assert.Null(persisted.ProcessingStartedAtUtc);

        var attempts = await new OutboxRepository(context)
            .GetAttemptsAsync(outbox.Id, CancellationToken.None);
        Assert.Single(attempts);
        Assert.True(attempts[0].Succeeded);
    }

    [Fact]
    public async Task Process_WhenProviderRejects_MarksOutboxFailedAndRetryable()
    {
        await using var context = CreateContext(nameof(Process_WhenProviderRejects_MarksOutboxFailedAndRetryable));

        var ring = DoorbellRingEvent.Create(Guid.NewGuid(), "evt-1", DateTime.UtcNow, "melody");
        var outbox = NotificationOutbox.Create(ring.Id, Guid.NewGuid());

        await context.DoorbellRingEvents.AddAsync(WithRowVersion(ring));
        await context.NotificationOutboxes.AddAsync(WithRowVersion(outbox));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, new RecordingDispatchService(success: false));

        await handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        var persisted = await context.NotificationOutboxes.FindAsync(outbox.Id);
        Assert.Equal(NotificationStatus.Failed, persisted!.Status);
        var candidates = await new OutboxRepository(context).GetUnprocessedAsync(
            10, 3, TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Single(candidates);
    }

    [Fact]
    public async Task Process_IsNotReprocessedOnceSent()
    {
        await using var context = CreateContext(nameof(Process_IsNotReprocessedOnceSent));

        var ring = DoorbellRingEvent.Create(Guid.NewGuid(), "evt-1", DateTime.UtcNow, "melody");
        var outbox = NotificationOutbox.Create(ring.Id, Guid.NewGuid());

        await context.DoorbellRingEvents.AddAsync(WithRowVersion(ring));
        await context.NotificationOutboxes.AddAsync(WithRowVersion(outbox));
        await context.SaveChangesAsync();

        var dispatch = new RecordingDispatchService(success: true);
        var handler = CreateHandler(context, dispatch);

        await handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);
        var second = await handler.Handle(new ProcessNotificationCommand(outbox.Id), CancellationToken.None);

        Assert.False(second);
        Assert.Equal(1, dispatch.Calls);
    }
}
