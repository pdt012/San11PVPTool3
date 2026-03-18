using System.Collections.Concurrent;
using System.Net.WebSockets;
using NLog;

namespace San11PVPToolServer.Services;

/// <summary>
/// </summary>
/// 只负责socket的管理，socket的创建和关闭应由上级调用者负责
public static class WebSocketManager
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private static ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> RoomPlayerSockets { get; } =
        new();

    public static ICollection<WebSocket> GetRoomSockets(string roomId)
    {
        if (!RoomPlayerSockets.TryGetValue(roomId, out var room)) return [];
        return room.Values;
    }

    public static void AddSocket(string roomId, string playerId, WebSocket socket)
    {
        var room = RoomPlayerSockets.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, WebSocket>());

        logger.Info($"player connected: {playerId}");
        room.AddOrUpdate(playerId, socket, (_, old) =>
        {
            try { old.Dispose(); }
            catch { }

            return socket;
        });
    }

    public static void RemoveSocket(string roomId, string playerId)
    {
        if (RoomPlayerSockets.TryGetValue(roomId, out var roomDict))
        {
            roomDict.TryRemove(playerId, out _);
            logger.Info($"player disconnected: {playerId}");

            if (roomDict.IsEmpty)
            {
                RoomPlayerSockets.TryRemove(roomId, out _);
            }
        }
    }

    public static WebSocket? GetSocket(string roomId, string playerId)
    {
        if (!RoomPlayerSockets.TryGetValue(roomId, out var room))
            return null;

        if (!room.TryGetValue(playerId, out var socket))
            return null;

        return socket;
    }
}
