namespace San11PVPToolShared.Models;

public record CreateRoomRequest(
    string PlayerName,
    RoomConfig Config
);

public record CreateRoomResponse(
    PlayerInfo UserInfo,
    RoomInfo RoomInfo,
    bool Success,
    string Message
);

public record JoinRoomRequest(
    string PlayerName,
    string RoomId,
    string? Password
);

public record JoinRoomResponse(
    PlayerInfo UserInfo,
    RoomInfo RoomInfo,
    bool Success,
    string Message
);

public record LeaveRoomRequest(
    string PlayerId,
    string RoomId
);

public record CloseRoomRequest(
    string PlayerId,
    string RoomId
);

public record KickPlayerRequest(
    string PlayerId,
    string RoomId,
    string TargetPlayerId
);

public record SetOwnerRequest(
    string PlayerId,
    string RoomId,
    string TargetPlayerId
);

public record SetKingNameRequest(
    string PlayerId,
    string RoomId,
    string TargetPlayerId,
    string KingName
);

public record SetRoomConfigRequest(
    string PlayerId,
    string RoomId,
    RoomConfig Config
);
