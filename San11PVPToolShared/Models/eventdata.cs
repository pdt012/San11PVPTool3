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
    SaveDataSummary? SaveDataSummary
);

public record SystemMessageEventData(
    string Message = ""
);
