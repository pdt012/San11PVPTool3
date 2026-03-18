namespace San11PVPToolShared.Models;

public record RoomConfig(
    string RoomName,
    string? Password,
    int MaxPlayers
);
