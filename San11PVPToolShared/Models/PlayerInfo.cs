namespace San11PVPToolShared.Models;

public record PlayerInfo(
    string PlayerId,
    string Name,
    PlayerRole Role,
    string KingName,
    bool Connected
)
{
    public string RoleDisplay => Role switch
    {
        PlayerRole.Owner => "房主",
        PlayerRole.Player => "一般",
        PlayerRole.Visitor => "旁观",
        _ => ""
    };

    public string StateBrush => Connected ? "Green" : "Red";

    public string ShortId => PlayerId.Length >= 4 ? PlayerId[..4] : PlayerId;
    public string NameDisplay => PlayerId != "" ? $"{Name} ({ShortId})" : "";
    public bool IsRoomOwner => Role == PlayerRole.Owner;
}
