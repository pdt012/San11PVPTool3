using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NLog;
using San11PVPToolServer.Models;
using San11PVPToolServer.Services;
using San11PVPToolShared.Events;
using San11PVPToolShared.Models;
using WebSocketManager = San11PVPToolServer.Services.WebSocketManager;

namespace San11PVPToolServer.ServerWebSocket;

public static class WebSocketHandler
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    public static async Task Handle(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var roomId = context.Request.Query["roomId"].ToString();

        var player = PlayerSessionAuth.Authenticate(context.Request, roomId);
        if (player == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();

        // 重连逻辑
        WebSocketConnection connection;
        lock (player)
        {
            connection = WebSocketManager.AddSocket(roomId, player.PlayerId, socket);
            player.IsConnected = true;
            player.LastHeartbeat = DateTime.Now;
        }

        await RoomEventDispatcher.SendToRoom(
            roomId, EventTypes.RoomInfoUpdated,
            new RoomInfoUpdatedEventData(RoomManager.GetRoomInfo(roomId)));

        await ReceiveLoop(player, connection);
    }

    private static async Task ReceiveLoop(Player player, WebSocketConnection connection)
    {
        var socket = connection.Socket;
        var buffer = new byte[1024];

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await connection.CloseOutputAsync("Closed by client");
                        break;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                lock (player)
                {
                    if (!WebSocketManager.IsCurrent(player.RoomId, player.PlayerId, connection))
                        break;

                    player.LastHeartbeat = DateTime.Now;
                }

                var msg = Encoding.UTF8.GetString(ms.ToArray());

                await ParseEvent(msg, player);
            }
        }
        catch (WebSocketException)
        {
            // 远端断网 / 崩溃
        }
        catch (IOException)
        {
            // 网络异常
        }
        catch (Exception)
        {
            // 其他异常
        }
        finally
        {
            bool removed;
            lock (player)
            {
                removed = WebSocketManager.RemoveSocket(player.RoomId, player.PlayerId, connection);
                if (removed)
                {
                    player.IsConnected = false;
                    player.LastDisconnectTime = DateTime.Now;
                }
            }

            if (removed)
            {
                await RoomEventDispatcher.SendToRoom(
                    player.RoomId, EventTypes.RoomInfoUpdated,
                    new RoomInfoUpdatedEventData(RoomManager.GetRoomInfo(player.RoomId)));
            }

            await connection.DisposeAsync();
        }
    }

    private static async Task ParseEvent(string json, Player player)
    {
        var evt = JsonSerializer.Deserialize<SocketEvent>(json);

        if (evt != null && handlers.TryGetValue(evt.Event, out var handler))
        {
            await RoomManager.EventActor.Enqueue(async () =>
            {
                if (handlers.TryGetValue(evt.Event, out var handler))
                {
                    await handler(player, (JsonElement)evt.Data);
                    s_logger.Debug($"get event: {evt.Event} player: {player.ShortId}");
                }
            });
        }
    }

    private static readonly Dictionary<string, Func<Player, JsonElement, Task>> handlers
        = new()
        {
            [EventTypes.ChatMessage] = async (player, data) =>
            {
                string msg = data.GetString();

                await RoomEventDispatcher.SendToRoom(player.RoomId, EventTypes.ChatMessage,
                    new ChatMessage(player.PlayerId, player.Name, msg, DateTime.Now));
            }
            // 注册其他事件
        };

    public static async Task CheckHeartbeat()
    {
        var now = DateTime.Now;
        var closeTasks = new List<Task>();
        foreach (var room in RoomManager.GetRooms())
        {
            foreach (var player in room.Players.Values)
            {
                if (!player.IsConnected) continue;
                if ((now - player.LastHeartbeat).TotalSeconds > 30)
                {
                    closeTasks.Add(HandleTimeout(player));
                }
            }
        }

        await Task.WhenAll(closeTasks);
    }

    private static async Task HandleTimeout(Player player)
    {
        var connection = WebSocketManager.GetConnection(player.RoomId, player.PlayerId);
        if (connection == null || connection.State != WebSocketState.Open)
            return;

        try
        {
            await connection.CloseOutputAsync("Heartbeat timeout");
        }
        catch (Exception ex)
        {
            s_logger.Warn(ex, $"Failed to close timed-out connection {connection.ConnectionId}");
        }
    }
}
