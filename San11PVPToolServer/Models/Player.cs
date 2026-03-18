using San11PVPToolShared.Models;

namespace San11PVPToolServer.Models;

public class Player
{
    public required string PlayerId { get; set; }

    public required string RoomId { get; set; }

    public required string Name { get; set; }

    public string KingName { get; set; } = "";

    public PlayerRole Role { get; set; }

    public DateTime LastHeartbeat { get; set; } = DateTime.Now;

    public bool IsConnected { get; set; } = true;

    public DateTime LastDisconnectTime { get; set; }

    public string ShortId => PlayerId.Length >= 4 ? PlayerId[..4] : PlayerId;

    public PlayerInfo ToDTO()
    {
        return new PlayerInfo(PlayerId, Name, Role, KingName, IsConnected);
    }
}
