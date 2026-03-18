using System.Collections.Concurrent;
using San11PVPToolShared.Models;

namespace San11PVPToolServer.Models;

public class Room
{
    public required string RoomId { get; init; }

    public required RoomConfig Config { get; set; }

    public ConcurrentDictionary<string, Player> Players { get; set; } = new();

    public string? SavePath { get; set; }

    public string ShortId => RoomId.Length >= 4 ? RoomId[..4] : RoomId;

    public RoomInfo ToDTO()
    {
        return new(RoomId, Config, Players.Values.Select(p => p.ToDTO()).ToList());
    }

    public RoomInfoSummary ToSummaryDTO()
    {
        return new RoomInfoSummary(RoomId, Config.RoomName,
            Players.Values.FirstOrDefault(p => p?.Role == PlayerRole.Owner)?.Name ?? "",
            !string.IsNullOrEmpty(Config.Password),
            $"({Players.Count}/{Config.MaxPlayers})");
    }
}
