using System.Collections.Concurrent;
using NLog;

namespace San11PVPToolServer.Services;

public static class SaveManager
{
    private const string SaveFolder = "saves";
    public const string DefaultFileName = "Save031.s11";

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SaveLocks = new();

    public static SemaphoreSlim GetLock(string roomId)
    {
        return SaveLocks.GetOrAdd(roomId, _ => new SemaphoreSlim(1, 1));
    }

    public static string GetSavePath(string roomId, string filename)
    {
        Directory.CreateDirectory(SaveFolder);

        return Path.Combine(SaveFolder, roomId, filename);
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
