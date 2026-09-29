namespace San11PVPToolShared.Models;

public record PublicRoomConfig(
    string RoomName,
    bool HasPassword,
    int MaxPlayers
);
