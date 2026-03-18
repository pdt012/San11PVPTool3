namespace San11PVPToolShared.Models;

public record RoomInfoSummary(
    string RoomId,
    string RoomName,
    string OwnerName,
    bool HasPassword,
    string PlayerCountDisplay
)
{
    public string ShortId => RoomId.Length >= 4 ? RoomId[^4..] : RoomId;
}
