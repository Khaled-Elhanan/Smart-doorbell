using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Domain.Entities;
using Doorbell.Infrastructure.Persistence.Context;
using Doorbell.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Doorbell.Tests.Infrastructure.Persistence;

public class UserDeviceTokenRepositoryTests
{
    private static ApplicationDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Add_AllowsMultipleTokensPerUser()
    {
        await using var context = CreateContext(nameof(Add_AllowsMultipleTokensPerUser));
        var userId = Guid.NewGuid();
        var repository = new UserDeviceTokenRepository(context);

        await repository.AddAsync(
            UserDeviceToken.Create(userId, "token-a", "android"),
            CancellationToken.None);
        await repository.AddAsync(
            UserDeviceToken.Create(userId, "token-b", "ios"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var tokens = await repository.GetByUserIdAsync(userId, CancellationToken.None);

        Assert.Equal(2, tokens.Count);
    }

    [Fact]
    public async Task Add_IsIdempotentForTheSameUserAndToken()
    {
        await using var context = CreateContext(nameof(Add_IsIdempotentForTheSameUserAndToken));
        var userId = Guid.NewGuid();
        var repository = new UserDeviceTokenRepository(context);

        await repository.AddAsync(
            UserDeviceToken.Create(userId, "token-a", "android"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        await repository.AddAsync(
            UserDeviceToken.Create(userId, "token-a", "android"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var tokens = await repository.GetByUserIdAsync(userId, CancellationToken.None);

        Assert.Single(tokens);
    }

    [Fact]
    public async Task Remove_DoesNotRemoveAnotherUsersToken()
    {
        await using var context = CreateContext(nameof(Remove_DoesNotRemoveAnotherUsersToken));
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var repository = new UserDeviceTokenRepository(context);

        await repository.AddAsync(
            UserDeviceToken.Create(owner, "owner-token", "android"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var removed = await repository.RemoveAsync(other, "owner-token", CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.False(removed);
        Assert.Single(await repository.GetByUserIdAsync(owner, CancellationToken.None));
    }

    [Fact]
    public async Task Remove_WhenOwnedToken_RemovesIt()
    {
        await using var context = CreateContext(nameof(Remove_WhenOwnedToken_RemovesIt));
        var owner = Guid.NewGuid();
        var repository = new UserDeviceTokenRepository(context);

        await repository.AddAsync(
            UserDeviceToken.Create(owner, "owner-token", "android"),
            CancellationToken.None);
        await context.SaveChangesAsync();

        var removed = await repository.RemoveAsync(owner, "owner-token", CancellationToken.None);
        await context.SaveChangesAsync();

        Assert.True(removed);
        Assert.Empty(await repository.GetByUserIdAsync(owner, CancellationToken.None));
    }
}
