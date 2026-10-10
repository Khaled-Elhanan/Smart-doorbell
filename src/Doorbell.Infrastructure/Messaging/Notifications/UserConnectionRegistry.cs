using System.Collections.Concurrent;

namespace Doorbell.Infrastructure.Messaging.Notifications;

public interface IUserConnectionRegistry
{
    void Add(Guid userId, string connectionId);

    void Remove(string connectionId);

    IReadOnlyList<string> GetConnections(Guid userId);

    bool IsConnected(Guid userId);
}

public sealed class UserConnectionRegistry : IUserConnectionRegistry
{
    private readonly ConcurrentDictionary<string, Guid> _connectionToUser = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _userToConnections = new();

    public void Add(Guid userId, string connectionId)
    {
        if (userId == Guid.Empty || string.IsNullOrEmpty(connectionId))
        {
            return;
        }

        _connectionToUser[connectionId] = userId;

        var connections = _userToConnections.GetOrAdd(
            userId,
            _ => new ConcurrentDictionary<string, byte>());

        connections[connectionId] = 0;
    }

    public void Remove(string connectionId)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return;
        }

        if (!_connectionToUser.TryRemove(connectionId, out var userId))
        {
            return;
        }

        if (_userToConnections.TryGetValue(userId, out var connections))
        {
            connections.TryRemove(connectionId, out _);

            if (connections.IsEmpty)
            {
                _userToConnections.TryRemove(userId, out _);
            }
        }
    }

    public IReadOnlyList<string> GetConnections(Guid userId)
    {
        if (_userToConnections.TryGetValue(userId, out var connections))
        {
            return connections.Keys.ToArray();
        }

        return Array.Empty<string>();
    }

    public bool IsConnected(Guid userId)
    {
        return _userToConnections.TryGetValue(userId, out var connections)
            && !connections.IsEmpty;
    }
}
