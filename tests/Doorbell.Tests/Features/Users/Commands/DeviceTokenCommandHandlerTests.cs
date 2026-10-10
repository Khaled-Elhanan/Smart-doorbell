using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Application.Abstractions.Security;
using Doorbell.Application.Features.Users.Commands.RegisterDeviceToken;
using Doorbell.Application.Features.Users.Commands.RemoveDeviceToken;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Exceptions;
using Moq;
using Xunit;

namespace Doorbell.Tests.Features.Users.Commands;

public class DeviceTokenCommandHandlerTests
{
    private readonly Mock<IUserDeviceTokenRepository> _tokenRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ICurrentUser> _currentUserMock = new();

    private void SetupAuthenticatedUser(Guid? userId)
    {
        _currentUserMock.SetupGet(u => u.IsAuthenticated).Returns(true);
        _currentUserMock
            .Setup(u => u.GetUserIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(userId);
    }

    [Fact]
    public async Task Register_ForAuthenticatedUser_PersistsTokenOwnedByThatUser()
    {
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        UserDeviceToken? captured = null;
        _tokenRepoMock
            .Setup(r => r.AddAsync(It.IsAny<UserDeviceToken>(), It.IsAny<CancellationToken>()))
            .Callback<UserDeviceToken, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        var handler = new RegisterDeviceTokenCommandHandler(
            _tokenRepoMock.Object, _uowMock.Object, _currentUserMock.Object);

        var id = await handler.Handle(
            new RegisterDeviceTokenCommand("fcm-token-1", "android"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(captured);
        Assert.Equal(userId, captured!.UserId);
        Assert.Equal("fcm-token-1", captured.Token);
        Assert.Equal("android", captured.Platform);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_Unauthenticated_ThrowsUnauthorized()
    {
        _currentUserMock.SetupGet(u => u.IsAuthenticated).Returns(false);

        var handler = new RegisterDeviceTokenCommandHandler(
            _tokenRepoMock.Object, _uowMock.Object, _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(
                new RegisterDeviceTokenCommand("fcm-token-1", "android"),
                CancellationToken.None));

        _tokenRepoMock.Verify(
            r => r.AddAsync(It.IsAny<UserDeviceToken>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_UnknownIdentity_ThrowsUnauthorized()
    {
        SetupAuthenticatedUser(null);

        var handler = new RegisterDeviceTokenCommandHandler(
            _tokenRepoMock.Object, _uowMock.Object, _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(
                new RegisterDeviceTokenCommand("fcm-token-1", "android"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Remove_ScopesDeletionToTheAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _tokenRepoMock
            .Setup(r => r.RemoveAsync(userId, "fcm-token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RemoveDeviceTokenCommandHandler(
            _tokenRepoMock.Object, _uowMock.Object, _currentUserMock.Object);

        var removed = await handler.Handle(
            new RemoveDeviceTokenCommand("fcm-token-1"),
            CancellationToken.None);

        Assert.True(removed);
        _tokenRepoMock.Verify(
            r => r.RemoveAsync(userId, "fcm-token-1", It.IsAny<CancellationToken>()),
            Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Remove_WhenTokenBelongsToAnotherUser_ReportsNotRemoved()
    {
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        _tokenRepoMock
            .Setup(r => r.RemoveAsync(userId, "someone-elses-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new RemoveDeviceTokenCommandHandler(
            _tokenRepoMock.Object, _uowMock.Object, _currentUserMock.Object);

        var removed = await handler.Handle(
            new RemoveDeviceTokenCommand("someone-elses-token"),
            CancellationToken.None);

        Assert.False(removed);
        _uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Remove_Unauthenticated_ThrowsUnauthorized()
    {
        _currentUserMock.SetupGet(u => u.IsAuthenticated).Returns(false);

        var handler = new RemoveDeviceTokenCommandHandler(
            _tokenRepoMock.Object, _uowMock.Object, _currentUserMock.Object);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(
                new RemoveDeviceTokenCommand("fcm-token-1"),
                CancellationToken.None));
    }
}
