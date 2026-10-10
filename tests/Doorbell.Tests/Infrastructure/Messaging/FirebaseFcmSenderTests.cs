using System;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Application.Abstractions.Messaging;
using Doorbell.Application.Abstractions.Persistence;
using Doorbell.Infrastructure.Messaging.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Doorbell.Tests.Infrastructure.Messaging;

public class FirebaseFcmSenderTests
{
    private static FirebaseFcmSender CreateSender(string? credentialsPath)
    {
        var options = Options.Create(new FirebaseMessagingOptions
        {
            CredentialsPath = credentialsPath
        });

        return new FirebaseFcmSender(
            options,
            Mock.Of<IUserDeviceTokenRepository>(),
            Mock.Of<ILogger<FirebaseFcmSender>>());
    }

    [Fact]
    public void IsConfigured_WhenCredentialsMissing_IsFalse()
    {
        var sender = CreateSender(@"Z:\does-not-exist\firebase-service-account.json");

        Assert.False(sender.IsConfigured);
        Assert.Contains("was not found", sender.ConfigurationStatus);
    }

    [Fact]
    public async Task SendAsync_WhenNotConfigured_ReportsFailure()
    {
        var sender = CreateSender(@"Z:\does-not-exist\firebase-service-account.json");

        var message = new NotificationMessage(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            null,
            null);

        var result = await sender.SendAsync(
            message.RecipientUserId,
            message,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not configured", result.ErrorMessage);
    }
}
