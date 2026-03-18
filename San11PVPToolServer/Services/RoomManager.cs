using System.Collections.Concurrent;
using NLog;
using San11PVPToolServer.Models;
using San11PVPToolServer.ServerWebSocket;
using San11PVPToolShared.Models;
using Player = San11PVPToolServer.Models.Player;

namespace San11PVPToolServer.Services;

/// <summary>
/// </summary>
/// 只提供修改房间和用户状态的接口，不发送任何事件，事件应由上级调用者负责发出
public static class RoomManager
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    public static RoomEventActor EventActor { get; } = new();

    private static ConcurrentDictionary<string, Room> Rooms { get; } = new();

    public static List<Room> GetRooms()
    {
        return Rooms.Values.ToList();
    }

    public static RoomInfo? GetRoomInfo(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null) return null;

        return new(room.RoomId, room.Config,
            room.Players.Values
                .Select(p => p.ToDTO())
                .ToList()
        );
    }

    public static Room? GetRoom(string roomId)
    {
        Rooms.TryGetValue(roomId, out var room);
        return room;
    }

    public static Player? GetPlayer(string roomId, string playerId)
    {
        var room = GetRoom(roomId);
        if (room == null) return null;

        room.Players.TryGetValue(playerId, out var player);
        return player;
    }

    public static Room AddRoom(RoomConfig config)
    {
        var room = new Room { RoomId = Guid.NewGuid().ToString(), Config = config };
        Rooms[room.RoomId] = room;
        logger.Debug($"room created: {room.ShortId}");
        return room;
    }

    public static void RemoveRoom(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        foreach (var player in room.Players.Values)
        {
            RemovePlayer(roomId, player.PlayerId);
        }

        RemoveRoomInternal(room);
    }

    private static void RemoveRoomInternal(Room room)
    {
        if (Rooms.TryRemove(room.RoomId, out var _))
            logger.Debug($"room removed: {room.ShortId}");
        _ = SaveManager.CleanupRoomAsync(room.RoomId);
    }

    public static Player? AddPlayer(string roomId, string playerName, PlayerRole role = PlayerRole.Player)
    {
        var room = GetRoom(roomId);
        if (room == null) return null;

        if (room.Players.Count >= room.Config.MaxPlayers)
            return null;

        var player = new Player
        {
            PlayerId = Guid.NewGuid().ToString(),
            RoomId = roomId,
            Name = playerName,
            Role = role,
            LastHeartbeat = DateTime.Now
        };
        room.Players[player.PlayerId] = player;
        logger.Debug($"player created: {player.ShortId} {player.Name}, room {room.ShortId}");

        return player;
    }

    public static void RemovePlayer(string roomId, string playerId)
    {
        var room = GetRoom(roomId);
        if (room == null) return;

        if (!room.Players.TryRemove(playerId, out var removed))
            return;
        logger.Debug($"player removed: {removed.ShortId} {removed.Name}");

        // 房间没人
        if (room.Players.IsEmpty)
        {
            RemoveRoomInternal(room);
        }
        // 如果房主退出
        else if (removed.Role == PlayerRole.Owner && !room.Players.IsEmpty)
        {
            var nextOwner = room.Players.Values
                .FirstOrDefault(p => p.Role == PlayerRole.Player) ?? room.Players.Values.First();

            nextOwner.Role = PlayerRole.Owner;
        }
    }

    public static void RemoveTimeoutPlayers()
    {
        var now = DateTime.Now;

        foreach (var room in Rooms.Values)
        {
            foreach (var player in room.Players.Values)
            {
                if (!player.IsConnected && (now - player.LastDisconnectTime).TotalSeconds > 30)
                {
                    RemovePlayer(player.RoomId, player.PlayerId);
                }
            }
        }
    }

    public static void CleanupRooms()
    {
        foreach (var room in Rooms.Values)
        {
            if (room.Players.IsEmpty)
            {
                Rooms.TryRemove(room.RoomId, out _);
            }
        }
    }
}
