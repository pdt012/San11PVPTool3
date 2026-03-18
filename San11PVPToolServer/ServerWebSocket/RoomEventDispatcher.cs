using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NLog;
using San11PVPToolShared.Events;
using WebSocketManager = San11PVPToolServer.Services.WebSocketManager;

namespace San11PVPToolServer.ServerWebSocket;

public static class RoomEventDispatcher
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public static async Task SendToRoom(string roomId, string eventType, object data, bool disconnectAfterSent = false)
    {
        var roomSockets = WebSocketManager.GetRoomSockets(roomId)
            .Where(s => s.State == WebSocketState.Open)
            .ToList();
        if (roomSockets.Count == 0)
            return;

        var evt = new SocketEvent(eventType, data);

        var json = JsonSerializer.Serialize(evt);
        var bytes = Encoding.UTF8.GetBytes(json);

        var tasks = roomSockets
            .Select(async s =>
            {
                try
                {
                    await s.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                    if (disconnectAfterSent)
                        await s.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected by server",
                            CancellationToken.None);
                }
                catch { }
            });

        await Task.WhenAll(tasks);

        logger.Debug($"Broadcasted to room {roomId[..4]}, {roomSockets.Count} sockets");
    }

    public static async Task SendToPlayer(string roomId, string playerId, string eventType, object data,
        bool disconnectAfterSent = false)
    {
        var socket = WebSocketManager.GetSocket(roomId, playerId);
        if (socket == null || socket.State != WebSocketState.Open)
            return;

        var evt = new SocketEvent(eventType, data);

        var json = JsonSerializer.Serialize(evt);
        var bytes = Encoding.UTF8.GetBytes(json);

        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            if (disconnectAfterSent)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected by server",
                    CancellationToken.None);
        }
        catch { }

        logger.Debug($"Send to player {playerId[..4]}");
    }
}
