using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Infrastructure.Persistence.Context;
using Doorbell.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Doorbell.Tests.Infrastructure.Persistence;

public class OutboxRepositoryTests
{
    private static ApplicationDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void SetProcessingStartedAt(NotificationOutbox outbox, DateTime value)
    {
        var field = typeof(NotificationOutbox).GetField(
            "<ProcessingStartedAtUtc>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        field.SetValue(outbox, (DateTime?)value);
    }

    private static T WithRowVersion<T>(T entity) where T : class
    {
        var field = typeof(T).GetField(
            "<RowVersion>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

        field?.SetValue(entity, new byte[] { 1 });

        return entity;
    }

    [Fact]
    public async Task GetUnprocessed_ReturnsPendingRows()
    {
        await using var context = CreateContext(nameof(GetUnprocessed_ReturnsPendingRows));
        var pending = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        await context.NotificationOutboxes.AddAsync(WithRowVersion(pending));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);

        var result = await repository.GetUnprocessedAsync(
            10,
            3,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(pending.Id, result[0].Id);
    }

    [Fact]
    public async Task GetUnprocessed_ExcludesSentRows()
    {
        await using var context = CreateContext(nameof(GetUnprocessed_ExcludesSentRows));
        var sent = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        sent.MarkProcessing();
        sent.MarkSent();

        await context.NotificationOutboxes.AddAsync(WithRowVersion(sent));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);

        var result = await repository.GetUnprocessedAsync(
            10,
            3,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnprocessed_IncludesFailedRowsBelowMaxAttempts()
    {
        await using var context = CreateContext(nameof(GetUnprocessed_IncludesFailedRowsBelowMaxAttempts));
        var failed = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        failed.MarkProcessing();
        failed.MarkFailed();

        await context.NotificationOutboxes.AddAsync(WithRowVersion(failed));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);

        var result = await repository.GetUnprocessedAsync(
            10,
            3,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(failed.Id, result[0].Id);
    }

    [Fact]
    public async Task GetUnprocessed_ExcludesFailedRowsAtMaxAttempts()
    {
        await using var context = CreateContext(nameof(GetUnprocessed_ExcludesFailedRowsAtMaxAttempts));
        var failed = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        for (var i = 0; i < 3; i++)
        {
            failed.MarkProcessing();
            failed.MarkFailed();
        }

        await context.NotificationOutboxes.AddAsync(WithRowVersion(failed));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);

        var result = await repository.GetUnprocessedAsync(
            10,
            3,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnprocessed_RecoversStrandedProcessingRows()
    {
        await using var context = CreateContext(nameof(GetUnprocessed_RecoversStrandedProcessingRows));
        var stranded = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        stranded.MarkProcessing();
        SetProcessingStartedAt(stranded, DateTime.UtcNow.AddMinutes(-30));

        await context.NotificationOutboxes.AddAsync(WithRowVersion(stranded));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);

        var result = await repository.GetUnprocessedAsync(
            10,
            3,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(stranded.Id, result[0].Id);
    }

    [Fact]
    public async Task GetUnprocessed_ExcludesFreshProcessingRows()
    {
        await using var context = CreateContext(nameof(GetUnprocessed_ExcludesFreshProcessingRows));
        var processing = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());
        processing.MarkProcessing();

        await context.NotificationOutboxes.AddAsync(WithRowVersion(processing));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);

        var result = await repository.GetUnprocessedAsync(
            10,
            3,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AddAttemptAndGetAttempts_RoundTrips()
    {
        await using var context = CreateContext(nameof(AddAttemptAndGetAttempts_RoundTrips));
        var outbox = NotificationOutbox.Create(Guid.NewGuid(), Guid.NewGuid());

        await context.NotificationOutboxes.AddAsync(WithRowVersion(outbox));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);

        await repository.AddAttemptAsync(
            NotificationAttempt.Create(outbox.Id, 1, true, null, NotificationChannel.SignalR),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var attempts = await repository.GetAttemptsAsync(outbox.Id, CancellationToken.None);

        Assert.Single(attempts);
        Assert.True(attempts[0].Succeeded);
        Assert.Equal(NotificationChannel.SignalR, attempts[0].Channel);
    }
}
