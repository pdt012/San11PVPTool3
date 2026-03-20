using Microsoft.AspNetCore.Mvc;
using NLog;
using San11PVPToolServer.ServerWebSocket;
using San11PVPToolServer.Services;
using San11PVPToolShared.Events;
using San11PVPToolShared.Models;
using San11PVPToolShared.Utils;

namespace San11PVPToolServer.Controllers;

[ApiController]
[Route("save")]
public class SaveController : ControllerBase
{
    private readonly IWebHostEnvironment _env;

    public SaveController(IWebHostEnvironment env)
    {
        _env = env;
    }

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] string playerId,
        [FromForm] string roomId,
        [FromForm] List<IFormFile> files)
    {
        // 验证权限
        var player = RoomManager.GetPlayer(roomId, playerId);
        if (player == null)
            return NotFound();
        if (player.Role < PlayerRole.Player)
            return Unauthorized();

        var lockObj = SaveManager.GetLock(roomId);
        await lockObj.WaitAsync();

        try
        {
            foreach (var file in files)
            {
                var path = SaveManager.GetSavePath(roomId, file.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                await using var stream = System.IO.File.Create(path);
                await file.CopyToAsync(stream);
            }

            // 分析存档
            SaveDataSummary? saveDataSummary = null;
            try
            {
                saveDataSummary = SaveDataParser.LoadSaveDataHeader(
                    SaveManager.GetSavePath(roomId, SaveManager.DefaultFileName));
            }
            catch (Exception ex)
            {
                s_logger.Error(ex, "Failed to load save data");
            }

            s_logger.Info($"{player.Name}上传存档.(君主:{saveDataSummary?.CurrentKingName ?? "??"})");
            _ = RoomEventDispatcher.SendToRoom(roomId, EventTypes.SaveUploaded,
                new SaveUploadedEventData(player.ToDTO(), saveDataSummary));
        }
        finally
        {
            lockObj.Release();
        }

        return Ok();
    }

    [HttpGet("list")]
    public IActionResult List(
        [FromQuery] string playerId,
        [FromQuery] string roomId,
        [FromQuery] string filename)
    {
        var player = RoomManager.GetPlayer(roomId, playerId);
        if (player == null)
            return NotFound();

        var baseSavePath = Path.Combine(_env.ContentRootPath,
            SaveManager.GetSavePath(roomId, filename));

        if (!System.IO.File.Exists(baseSavePath))
            return NotFound();

        List<string> files = [];
        foreach (var extension in new[] { "s11", ".exsav", ".xml" })
        {
            var exPath = Path.ChangeExtension(baseSavePath, extension);
            if (System.IO.File.Exists(exPath))
                files.Add(Path.GetFileName(exPath));
        }

        return Ok(files);
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download(
        [FromQuery] string playerId,
        [FromQuery] string roomId,
        [FromQuery] string filename)
    {
        var player = RoomManager.GetPlayer(roomId, playerId);
        if (player == null)
            return NotFound();

        var lockObj = SaveManager.GetLock(roomId);
        await lockObj.WaitAsync();

        try
        {
            var path = Path.Combine(_env.ContentRootPath,
                SaveManager.GetSavePath(roomId, filename));

            if (!System.IO.File.Exists(path))
                return NotFound();

            s_logger.Info($"{player.Name}下载存档 {filename}");
            if (filename == SaveManager.DefaultFileName)
            {
                _ = RoomEventDispatcher.SendToRoom(roomId, EventTypes.SystemMessage,
                    new SystemMessageEventData($"{player.Name}下载了存档"));
            }

            return PhysicalFile(path, "application/octet-stream");
        }
        finally
        {
            lockObj.Release();
        }
    }
}
