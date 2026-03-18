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
        var playerId = context.Request.Query["playerId"].ToString();

        var player = RoomManager.GetPlayer(roomId, playerId);
        if (player == null)
        {
            context.Response.StatusCode = 404;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();

        // 重连逻辑
        player.IsConnected = true;

        WebSocketManager.AddSocket(roomId, playerId, socket);
        await RoomEventDispatcher.SendToRoom(
            roomId, EventTypes.RoomInfoUpdated,
            new RoomInfoUpdatedEventData(RoomManager.GetRoomInfo(roomId)));

        await ReceiveLoop(player, socket);
    }

    private static async Task ReceiveLoop(Player player, WebSocket socket)
    {
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
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closed by client",
                            CancellationToken.None);
                        break;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                player.LastHeartbeat = DateTime.Now;

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
            player.IsConnected = false;
            player.LastDisconnectTime = DateTime.Now;

            WebSocketManager.RemoveSocket(player.RoomId, player.PlayerId);
            await RoomEventDispatcher.SendToRoom(
                player.RoomId, EventTypes.RoomInfoUpdated,
                new RoomInfoUpdatedEventData(RoomManager.GetRoomInfo(player.RoomId)));

            socket.Dispose();
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

                _ = RoomEventDispatcher.SendToRoom(player.RoomId, EventTypes.ChatMessage,
                    new ChatMessage(player.PlayerId, player.Name, msg, DateTime.Now));
            }
            // 注册其他事件
        };

    public static void CheckHeartbeat()
    {
        var now = DateTime.Now;
        foreach (var room in RoomManager.GetRooms())
        {
            foreach (var player in room.Players.Values)
            {
                if (!player.IsConnected) continue;
                if ((now - player.LastHeartbeat).TotalSeconds > 30)
                {
                    HandleTimeout(player);
                }
            }
        }
    }

    private static void HandleTimeout(Player player)
    {
        var socket = WebSocketManager.GetSocket(player.RoomId, player.PlayerId);
        if (socket == null) return;
        if (socket.State != WebSocketState.Open) return;
        try
        {
            socket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Heartbeat timeout",
                CancellationToken.None);
        }
        catch
        {
            // ignored
        }
    }
}
