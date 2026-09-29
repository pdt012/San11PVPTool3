using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NLog;
using San11PVPToolShared.Events;
using WebSocketManager = San11PVPToolServer.Services.WebSocketManager;

namespace San11PVPToolServer.ServerWebSocket;

public static class RoomEventDispatcher
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static async Task SendToRoom(string roomId, string eventType, object data, bool disconnectAfterSent = false)
    {
        var roomConnections = WebSocketManager.GetRoomConnections(roomId)
            .Where(connection => connection.State == WebSocketState.Open)
            .ToList();
        if (roomConnections.Count == 0)
            return;

        var evt = new SocketEvent(eventType, data);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

        var tasks = roomConnections.Select(async connection =>
        {
            try
            {
                await connection.SendAsync(bytes, disconnectAfterSent);
            }
            catch (Exception ex)
            {
                s_logger.Warn(ex,
                    $"Failed to send event {eventType} to connection {connection.ConnectionId}");
            }
        });

        await Task.WhenAll(tasks);

        s_logger.Debug($"Broadcasted to room {roomId[..4]}, {roomConnections.Count} sockets");
    }

    public static async Task SendToPlayer(string roomId, string playerId, string eventType, object data,
        bool disconnectAfterSent = false)
    {
        var connection = WebSocketManager.GetConnection(roomId, playerId);
        if (connection == null || connection.State != WebSocketState.Open)
            return;

        var evt = new SocketEvent(eventType, data);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evt));

        try
        {
            await connection.SendAsync(bytes, disconnectAfterSent);
            s_logger.Debug($"Send to player {playerId[..4]}");
        }
        catch (Exception ex)
        {
            s_logger.Warn(ex,
                $"Failed to send event {eventType} to player {playerId[..4]}, connection {connection.ConnectionId}");
        }
    }
}
