namespace San11PVPToolShared.Models;

public record RoomInfo(
    string RoomId,
    RoomConfig Config,
    List<PlayerInfo> Players
)
{
    public string Name => Config.RoomName;
    public string ShortId => RoomId.Length >= 4 ? RoomId[..4] : RoomId;
    public string PlayerCountDisplay => $"({Players.Count}/{Config.MaxPlayers})";
    public string OwnerName => Players.Find(p => p.Role == PlayerRole.Owner)?.Name ?? "";
}
