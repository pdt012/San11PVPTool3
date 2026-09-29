using System.Collections.Concurrent;
using NLog;

namespace San11PVPToolServer.Services;

public static class SaveManager
{
    private const string SaveFolder = "saves";
    public const string DefaultFileName = "Save031.s11";

    private static readonly HashSet<string> AllowedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        DefaultFileName,
        "Save031.exsav",
        "Save031.xml"
    };

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SaveLocks = new();

    public static SemaphoreSlim GetLock(string roomId)
    {
        return SaveLocks.GetOrAdd(roomId, _ => new SemaphoreSlim(1, 1));
    }

    public static string GetSavePath(string roomId, string filename)
    {
        if (!TryGetSavePath(roomId, filename, out var path))
            throw new ArgumentException("Invalid room ID or save file name.");

        return path;
    }

    public static bool TryGetSavePath(string roomId, string filename, out string path)
    {
        path = "";
        if (!Guid.TryParse(roomId, out _) ||
            string.IsNullOrWhiteSpace(filename) ||
            !string.Equals(Path.GetFileName(filename), filename, StringComparison.Ordinal) ||
            !AllowedFileNames.Contains(filename))
            return false;

        var roomDirectory = Path.GetFullPath(Path.Combine(SaveFolder, roomId));
        var candidate = Path.GetFullPath(Path.Combine(roomDirectory, filename));
        var roomPrefix = Path.TrimEndingDirectorySeparator(roomDirectory) + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(roomPrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        Directory.CreateDirectory(SaveFolder);
        path = Path.Combine(SaveFolder, roomId, filename);
        return true;
    }

    public static async Task CleanupRoomAsync(string roomId)
    {
        if (SaveLocks.TryRemove(roomId, out var sem))
        {
            try
            {
                await sem.WaitAsync();
                // 删除保存目录
                var dir = Path.Combine(SaveFolder, roomId);
                if (Directory.Exists(dir))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (IOException ex)
                    {
                        s_logger.Error($"Failed to delete room folder {dir}: {ex.Message}");
                    }
                }
            }
            finally
            {
                sem.Release();
                sem.Dispose();
            }
        }
    }
}
