using System.Collections.Concurrent;
using System.Net.WebSockets;
using NLog;

namespace San11PVPToolServer.Services;

/// <summary>
/// 管理当前有效的 WebSocket 连接。连接的创建、接收循环和最终释放由上级调用者负责。
/// </summary>
public static class WebSocketManager
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly ConcurrentDictionary<(string RoomId, string PlayerId), WebSocketConnection>
        Connections = new();

    public static IReadOnlyList<WebSocketConnection> GetRoomConnections(string roomId)
    {
        return Connections
            .Where(pair => pair.Key.RoomId == roomId)
            .Select(pair => pair.Value)
            .ToList();
    }

    public static WebSocketConnection AddSocket(string roomId, string playerId, WebSocket socket)
    {
        var key = (roomId, playerId);
        var connection = new WebSocketConnection(socket);

        while (true)
        {
            if (!Connections.TryGetValue(key, out var oldConnection))
            {
                if (Connections.TryAdd(key, connection))
                    break;

                continue;
            }

            if (!Connections.TryUpdate(key, connection, oldConnection))
                continue;

            oldConnection.Abort();
            break;
        }

        s_logger.Info($"player connected: {playerId}, connection: {connection.ConnectionId}");
        return connection;
    }

    public static bool RemoveSocket(string roomId, string playerId, WebSocketConnection connection)
    {
        var pair = new KeyValuePair<(string RoomId, string PlayerId), WebSocketConnection>(
            (roomId, playerId), connection);
        var removed = ((ICollection<KeyValuePair<(string RoomId, string PlayerId), WebSocketConnection>>)
            Connections).Remove(pair);

        if (removed)
            s_logger.Info($"player disconnected: {playerId}, connection: {connection.ConnectionId}");

        return removed;
    }

    public static bool IsCurrent(string roomId, string playerId, WebSocketConnection connection)
    {
        return Connections.TryGetValue((roomId, playerId), out var current) &&
               ReferenceEquals(current, connection);
    }

    public static WebSocketConnection? GetConnection(string roomId, string playerId)
    {
        Connections.TryGetValue((roomId, playerId), out var connection);
        return connection;
    }
}
