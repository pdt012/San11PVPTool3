using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using NLog;
using San11PVPToolServer.ServerWebSocket;
using San11PVPToolServer.Services;
using San11PVPToolShared.Events;
using San11PVPToolShared.Models;
using WebSocketManager = San11PVPToolServer.Services.WebSocketManager;

namespace San11PVPToolServer.Controllers;

[ApiController]
[Route("room")]
public class RoomController : ControllerBase
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    [HttpPost("create")]
    public async Task<IActionResult> CreateAsync([FromBody] CreateRoomRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            var room = RoomManager.AddRoom(req.Config);
            _ = RoomManager.AddPlayer(room.RoomId, req.PlayerName, PlayerRole.Owner);

            var owner = room.Players.Values.First();

            return Task.FromResult(
                Ok(new CreateRoomResponse(owner.ToDTO(), room.ToDTO(), true, "")));
        });
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinRoomRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            var room = RoomManager.GetRoom(req.RoomId);

            if (room == null)
                return Task.FromResult(
                    Ok(new JoinRoomResponse(null, null, false, "房间不存在")));

            if (!string.IsNullOrEmpty(room.Config.Password) &&
                room.Config.Password != req.Password)
                return Task.FromResult(
                    Ok(new JoinRoomResponse(null, null, false, "密码错误")));

            var player = RoomManager.AddPlayer(req.RoomId, req.PlayerName);

            if (player == null)
                return Task.FromResult(
                    Ok(new JoinRoomResponse(null, null, false, "房间已满")));

            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.RoomInfoUpdated,
                new RoomInfoUpdatedEventData(
                    RoomManager.GetRoomInfo(req.RoomId),
                    $"{player.Name}加入房间"
                ));

            return Task.FromResult(
                Ok(new JoinRoomResponse(player.ToDTO(), room.ToDTO(), true, "")));
        });
    }

    [HttpGet("info")]
    public async Task<IActionResult> Info(string roomId)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            var room = RoomManager.GetRoom(roomId);
            if (room == null)
                return Task.FromResult<IActionResult>(NotFound());
            return Task.FromResult<IActionResult>(Ok(room.ToDTO()));
        });
    }

    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            return Task.FromResult(
                Ok(RoomManager.GetRooms().Select(r => r.ToSummaryDTO()).ToList()));
        });
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave([FromBody] LeaveRoomRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            var player = RoomManager.GetPlayer(req.RoomId, req.PlayerId);
            if (player == null)
                return Task.FromResult(Ok());
            logger.Info($"player({player.ShortId}) leave room({req.RoomId[..4]})");

            RoomManager.RemovePlayer(req.RoomId, req.PlayerId);

            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.RoomInfoUpdated,
                new RoomInfoUpdatedEventData(
                    RoomManager.GetRoomInfo(req.RoomId),
                    $"{player.Name}离开房间"
                ));

            return Task.FromResult(Ok());
        });
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseRoomRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            // 验证权限
            var player = RoomManager.GetPlayer(req.RoomId, req.PlayerId);
            if (player == null)
                return Task.FromResult<IActionResult>(NotFound());
            if (player.Role != PlayerRole.Owner)
                return Task.FromResult<IActionResult>(Unauthorized());

            logger.Info($"player({req.PlayerId[..4]}) close room({req.RoomId[..4]})");

            // 通知并关闭所有用户的连接
            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.RoomClosed, "",
                disconnectAfterSent: true);

            // 清除房间
            RoomManager.RemoveRoom(req.RoomId);

            return Task.FromResult<IActionResult>(Ok());
        });
    }

    [HttpPost("kick")]
    public async Task<IActionResult> Kick([FromBody] KickPlayerRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            // 验证权限
            var player = RoomManager.GetPlayer(req.RoomId, req.PlayerId);
            if (player == null)
                return Task.FromResult(Ok());
            if (player.Role != PlayerRole.Owner)
                return Task.FromResult(Ok());

            var kicked = RoomManager.GetPlayer(req.RoomId, req.TargetPlayerId);
            if (kicked == null)
                return Task.FromResult(Ok());

            logger.Info($"player({player.ShortId}) kick ({kicked.ShortId})");
            // 断开连接
            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.PlayerKicked,
                new PlayerKickedEventData(kicked.ToDTO(), player.ToDTO()));
            var socket = WebSocketManager.GetSocket(req.RoomId, req.TargetPlayerId);
            socket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected by server", CancellationToken.None);
            // 清除该用户
            RoomManager.RemovePlayer(req.RoomId, req.TargetPlayerId);
            // 更新房间信息
            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.RoomInfoUpdated,
                new RoomInfoUpdatedEventData(RoomManager.GetRoomInfo(req.RoomId)));

            return Task.FromResult(Ok());
        });
    }

    [HttpPost("set-owner")]
    public async Task<IActionResult> SetOwner([FromBody] SetOwnerRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            // 验证权限
            var player = RoomManager.GetPlayer(req.RoomId, req.PlayerId);
            if (player == null)
                return Task.FromResult(Ok());
            if (player.Role != PlayerRole.Owner)
                return Task.FromResult(Ok());

            var newOwner = RoomManager.GetPlayer(req.RoomId, req.TargetPlayerId);
            if (newOwner == null)
                return Task.FromResult(Ok());

            logger.Info($"player({player.ShortId}) set ({newOwner.ShortId}) as new owner");
            // 重设身份
            player.Role = PlayerRole.Player;
            newOwner.Role = PlayerRole.Owner;

            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.RoomInfoUpdated,
                new RoomInfoUpdatedEventData(
                    RoomManager.GetRoomInfo(req.RoomId),
                    $"{player.Name}将{newOwner.Name}设置为新房主"
                ));

            return Task.FromResult(Ok());
        });
    }

    [HttpPost("set-king-name")]
    public async Task<IActionResult> SetKingName([FromBody] SetKingNameRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            // 验证权限
            var player = RoomManager.GetPlayer(req.RoomId, req.PlayerId);
            if (player == null)
                return Task.FromResult(Ok());
            var targetPlayer = RoomManager.GetPlayer(req.RoomId, req.TargetPlayerId);
            if (targetPlayer == null)
                return Task.FromResult(Ok());
            if (player.Role < targetPlayer.Role)
                return Task.FromResult(Ok());

            logger.Info($"player({player.ShortId}) set ({targetPlayer.ShortId})'s king name: {req.KingName}");
            // 设置君主名
            targetPlayer.KingName = req.KingName;

            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.RoomInfoUpdated,
                new RoomInfoUpdatedEventData(
                    RoomManager.GetRoomInfo(req.RoomId),
                    $"{player.Name}将{targetPlayer.Name}的君主名更改为[{targetPlayer.KingName}]"
                ));

            return Task.FromResult(Ok());
        });
    }

    [HttpPost("set-config")]
    public async Task<IActionResult> SetRoomConfig([FromBody] SetRoomConfigRequest req)
    {
        return await RoomManager.EventActor.Enqueue(() =>
        {
            // 验证权限
            var player = RoomManager.GetPlayer(req.RoomId, req.PlayerId);
            if (player == null)
                return Task.FromResult(Ok());
            if (player.Role != PlayerRole.Owner)
                return Task.FromResult(Ok());
            var room = RoomManager.GetRoom(req.RoomId);
            if (room == null)
                return Task.FromResult(Ok());

            logger.Info($"player({player.ShortId}) update room({room.ShortId}) config");

            room.Config = req.Config;

            _ = RoomEventDispatcher.SendToRoom(req.RoomId, EventTypes.RoomInfoUpdated,
                new RoomInfoUpdatedEventData(
                    RoomManager.GetRoomInfo(req.RoomId),
                    $"{player.Name}更新了房间信息"
                ));

            return Task.FromResult(Ok());
        });
    }
}
