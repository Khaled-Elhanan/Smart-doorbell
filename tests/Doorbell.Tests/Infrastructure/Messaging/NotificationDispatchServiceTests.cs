using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Domain.Entities;
using Doorbell.Domain.Enums;
using Doorbell.Infrastructure.Messaging.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Doorbell.Tests.Infrastructure.Messaging;

public class NotificationDispatchServiceTests
{
    private readonly Mock<IUserConnectionRegistry> _connections = new();
    private readonly Mock<ISignalRNotificationSender> _signalR = new();
    private readonly Mock<IFcmNotificationSender> _fcm = new();
    private readonly Mock<IUserDeviceTokenRepository> _tokens = new();

    private readonly Guid _recipient = Guid.NewGuid();

    private NotificationDispatchService CreateService(bool fallbackToFcm = true)
    {
        return new NotificationDispatchService(
            _connections.Object,
            _signalR.Object,
            _fcm.Object,
            _tokens.Object,
            Options.Create(new NotificationOutboxOptions { FallbackToFcm = fallbackToFcm }),
            Mock.Of<ILogger<NotificationDispatchService>>());
    }

    private NotificationMessage CreateMessage()
    {
        return new NotificationMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            _recipient,
            Guid.NewGuid(),
            DateTime.UtcNow,
            "snapshots/key.jpg",
            "640x480");
    }

    private void WithTokens(params UserDeviceToken[] tokens)
    {
        _tokens.Setup(t => t.GetByUserIdAsync(_recipient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);
    }

    [Fact]
    public async Task Dispatch_WhenConnectedAndSignalRSucceeds_UsesSignalR()
    {
        _connections.Setup(c => c.IsConnected(_recipient)).Returns(true);
        _signalR.Setup(s => s.SendAsync(_recipient, It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult(true, false, null));

        var result = await CreateService().DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(NotificationChannel.SignalR, result.Channel);
        Assert.False(result.UsedFallback);
        _fcm.Verify(f => f.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_WhenNotConnectedAndTokenExists_UsesFcm()
    {
        _connections.Setup(c => c.IsConnected(_recipient)).Returns(false);
        WithTokens(UserDeviceToken.Create(_recipient, "token-1", "android"));
        _fcm.SetupGet(f => f.IsConfigured).Returns(true);
        _fcm.Setup(f => f.SendAsync(_recipient, It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult(true, false, null));

        var result = await CreateService().DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(NotificationChannel.Fcm, result.Channel);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public async Task Dispatch_WhenNotConnectedAndNoToken_FailsHonestly()
    {
        _connections.Setup(c => c.IsConnected(_recipient)).Returns(false);
        WithTokens();

        var result = await CreateService().DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Channel);
        Assert.Contains("no FCM device token", result.ErrorMessage);
        _fcm.Verify(f => f.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_WhenSignalRFailsAndTokenExists_FallsBackToFcm()
    {
        _connections.Setup(c => c.IsConnected(_recipient)).Returns(true);
        _signalR.Setup(s => s.SendAsync(_recipient, It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult(false, true, "socket closed"));
        WithTokens(UserDeviceToken.Create(_recipient, "token-1", "ios"));
        _fcm.SetupGet(f => f.IsConfigured).Returns(true);
        _fcm.Setup(f => f.SendAsync(_recipient, It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult(true, false, null));

        var result = await CreateService().DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(NotificationChannel.Fcm, result.Channel);
        Assert.True(result.UsedFallback);
    }

    [Fact]
    public async Task Dispatch_WhenSignalRFailsAndNoToken_FailsWithSignalRChannel()
    {
        _connections.Setup(c => c.IsConnected(_recipient)).Returns(true);
        _signalR.Setup(s => s.SendAsync(_recipient, It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult(false, true, "socket closed"));
        WithTokens();

        var result = await CreateService().DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NotificationChannel.SignalR, result.Channel);
        Assert.Contains("socket closed", result.ErrorMessage);
    }

    [Fact]
    public async Task Dispatch_WhenFcmRequiredButNotConfigured_ReportsConfigurationFailure()
    {
        _connections.Setup(c => c.IsConnected(_recipient)).Returns(false);
        WithTokens(UserDeviceToken.Create(_recipient, "token-1", "android"));
        _fcm.SetupGet(f => f.IsConfigured).Returns(false);
        _fcm.SetupGet(f => f.ConfigurationStatus).Returns("Firebase credentials are not configured.");

        var result = await CreateService().DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NotificationChannel.Fcm, result.Channel);
        Assert.Contains("not configured", result.ErrorMessage);
    }

    [Fact]
    public async Task Dispatch_WhenFallbackDisabled_DoesNotAttemptFcm()
    {
        _connections.Setup(c => c.IsConnected(_recipient)).Returns(true);
        _signalR.Setup(s => s.SendAsync(_recipient, It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationSendResult(false, true, "socket closed"));
        WithTokens(UserDeviceToken.Create(_recipient, "token-1", "android"));

        var result = await CreateService(fallbackToFcm: false)
            .DispatchAsync(CreateMessage(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(NotificationChannel.SignalR, result.Channel);
        _fcm.Verify(f => f.SendAsync(It.IsAny<Guid>(), It.IsAny<NotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
