namespace San11PVPToolShared.Models;

public record PlayerKickedEventData(
    PlayerInfo KickedPlayer,
    PlayerInfo ByPlayer
);

public record RoomInfoUpdatedEventData(
    RoomInfo? RoomInfo,
    string Message = ""
);

public record SaveUploadedEventData(
    PlayerInfo Player,
    int CurrentKingId,
    string CurrentKingName
);

public record SystemMessageEventData(
    string Message = ""
);
