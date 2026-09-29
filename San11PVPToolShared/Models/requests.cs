namespace San11PVPToolShared.Models;

public record CreateRoomRequest(
    string PlayerName,
    RoomConfig Config
);

public record CreateRoomResponse(
    PlayerInfo UserInfo,
    RoomInfo RoomInfo,
    string SessionToken,
    bool Success,
    string Message
);

public record JoinRoomRequest(
    string PlayerName,
    string RoomId,
    string? Password
);

public record JoinRoomResponse(
    PlayerInfo? UserInfo,
    RoomInfo? RoomInfo,
    string? SessionToken,
    bool Success,
    string Message
);

public record LeaveRoomRequest(
    string RoomId
);

public record CloseRoomRequest(
    string RoomId
);

public record KickPlayerRequest(
    string RoomId,
    string TargetPlayerId
);

public record SetOwnerRequest(
    string RoomId,
    string TargetPlayerId
);

public record SetKingNameRequest(
    string RoomId,
    string TargetPlayerId,
    string KingName
);

public record SetRoomConfigRequest(
    string RoomId,
    RoomConfig Config
);
