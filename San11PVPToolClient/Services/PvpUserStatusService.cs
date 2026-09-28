using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Services;

public static class PvpUserStatusService
{
    public const string FileName = "PVPUserStatus";

    private static readonly object s_writeLock = new();
    private static readonly Encoding s_utf8WithoutBom = new UTF8Encoding(false);

    public static void Write(
        string saveDataDir,
        bool isOnline,
        int myForceId,
        int currentTurnPlayerForceId,
        IEnumerable<PlayerInfo> roomPlayers,
        IReadOnlyDictionary<string, int> playerForceIds)
    {
        if (string.IsNullOrWhiteSpace(saveDataDir) || !Directory.Exists(saveDataDir))
            return;

        var players = roomPlayers
            .Where(player => player.Role >= PlayerRole.Player)
            .ToList();
        var heartbeat = DateTimeOffset.Now.ToUniversalTime();

        List<string> lines =
        [
            "# 全局状态",
            "# 0-单机, 1-联机",
            $"game_status={(isOnline ? 1 : 0)}",
            $"heartbeat_ts={heartbeat.ToUnixTimeSeconds()}",
            CreateHeartbeatUtcLine(heartbeat),
            $"my_force_id={myForceId}",
            $"current_turn_player_force_id={currentTurnPlayerForceId}",
            $"player_count={players.Count}"
        ];

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var forceId = playerForceIds.GetValueOrDefault(player.PlayerId, -1);
            lines.Add("");
            lines.Add($"# 玩家{i} 数据 (前缀 p{i}_)");
            lines.Add($"p{i}_id={i}");
            lines.Add($"p{i}_name={SanitizeValue(player.Name)}");
            lines.Add($"p{i}_force_id={forceId}");
            if (i == 0)
                lines.Add("# 0-离线, 1-在线");
            lines.Add($"p{i}_online_status={(player.Connected ? 1 : 0)}");
        }

        var targetPath = Path.Combine(saveDataDir, FileName);
        var tempPath = targetPath + ".tmp";
        var content = string.Join("\r\n", lines);

        lock (s_writeLock)
        {
            try
            {
                File.WriteAllText(tempPath, content, s_utf8WithoutBom);
                File.Move(tempPath, targetPath, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }

    public static void UpdateHeartbeat(string saveDataDir)
    {
        if (string.IsNullOrWhiteSpace(saveDataDir) || !Directory.Exists(saveDataDir))
            return;

        var targetPath = Path.Combine(saveDataDir, FileName);
        if (!File.Exists(targetPath))
            return;

        var tempPath = targetPath + ".tmp";

        lock (s_writeLock)
        {
            try
            {
                var lines = File.ReadAllLines(targetPath, s_utf8WithoutBom).ToList();
                var heartbeat = DateTimeOffset.Now.ToUniversalTime();
                var heartbeatTsLine = $"heartbeat_ts={heartbeat.ToUnixTimeSeconds()}";
                var heartbeatUtcLine = CreateHeartbeatUtcLine(heartbeat);
                var heartbeatTsIndex = lines.FindIndex(line => line.StartsWith("heartbeat_ts=", StringComparison.Ordinal));
                var heartbeatUtcIndex = lines.FindIndex(line => line.StartsWith("heartbeat_utc=", StringComparison.Ordinal));

                if (heartbeatTsIndex >= 0)
                {
                    lines[heartbeatTsIndex] = heartbeatTsLine;
                }
                else
                {
                    var gameStatusIndex = lines.FindIndex(line => line.StartsWith("game_status=", StringComparison.Ordinal));
                    heartbeatTsIndex = gameStatusIndex >= 0 ? gameStatusIndex + 1 : 0;
                    lines.Insert(heartbeatTsIndex, heartbeatTsLine);
                    if (heartbeatUtcIndex >= heartbeatTsIndex)
                        heartbeatUtcIndex++;
                }

                if (heartbeatUtcIndex >= 0)
                    lines[heartbeatUtcIndex] = heartbeatUtcLine;
                else
                    lines.Insert(heartbeatTsIndex + 1, heartbeatUtcLine);

                File.WriteAllText(tempPath, string.Join("\r\n", lines), s_utf8WithoutBom);
                File.Move(tempPath, targetPath, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }

    private static string CreateHeartbeatUtcLine(DateTimeOffset heartbeat)
    {
        return $"heartbeat_utc={heartbeat.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)}";
    }

    private static string SanitizeValue(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
