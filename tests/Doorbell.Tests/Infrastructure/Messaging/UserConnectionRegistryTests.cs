using System;
using Doorbell.Infrastructure.Messaging.Notifications;
using Xunit;

namespace Doorbell.Tests.Infrastructure.Messaging;

public class UserConnectionRegistryTests
{
    [Fact]
    public void Add_TracksMultipleConnectionsForOneUser()
    {
        var registry = new UserConnectionRegistry();
        var userId = Guid.NewGuid();

        registry.Add(userId, "connection-1");
        registry.Add(userId, "connection-2");

        Assert.True(registry.IsConnected(userId));
        Assert.Equal(2, registry.GetConnections(userId).Count);
    }

    [Fact]
    public void Remove_OnlyRemovesTheSpecifiedConnection()
    {
        var registry = new UserConnectionRegistry();
        var userId = Guid.NewGuid();

        registry.Add(userId, "connection-1");
        registry.Add(userId, "connection-2");

        registry.Remove("connection-1");

        Assert.True(registry.IsConnected(userId));
        Assert.Single(registry.GetConnections(userId));
        Assert.Contains("connection-2", registry.GetConnections(userId));
    }

    [Fact]
    public void Remove_LastConnection_ReportsDisconnected()
    {
        var registry = new UserConnectionRegistry();
        var userId = Guid.NewGuid();

        registry.Add(userId, "connection-1");
        registry.Remove("connection-1");

        Assert.False(registry.IsConnected(userId));
        Assert.Empty(registry.GetConnections(userId));
    }

    [Fact]
    public void GetConnections_ForUnknownUser_IsEmpty()
    {
        var registry = new UserConnectionRegistry();

        Assert.False(registry.IsConnected(Guid.NewGuid()));
        Assert.Empty(registry.GetConnections(Guid.NewGuid()));
    }

    [Fact]
    public void Add_WithEmptyUser_IsIgnored()
    {
        var registry = new UserConnectionRegistry();

        registry.Add(Guid.Empty, "connection-1");

        Assert.False(registry.IsConnected(Guid.Empty));
    }
}
