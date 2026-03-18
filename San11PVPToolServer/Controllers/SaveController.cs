using System.Text;
using Microsoft.AspNetCore.Mvc;
using NLog;
using San11PVPToolServer.ServerWebSocket;
using San11PVPToolServer.Services;
using San11PVPToolShared.Events;
using San11PVPToolShared.Models;

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
            string kingName = "";
            sbyte kingId = -1;
            try
            {
                await using var fs = new FileStream(
                    SaveManager.GetSavePath(roomId, SaveManager.DefaultFileName),
                    FileMode.Open, FileAccess.Read);
                byte[] headData = new byte[0x64];
                int bytesRead = fs.Read(headData, 0, headData.Length);

                if (bytesRead >= 0x64) // 确保读取长度足够
                {
                    byte[] nameData = new byte[0x63 - 0x5A];
                    Array.Copy(headData, 0x5A, nameData, 0, nameData.Length);

                    byte idData = headData[0x63];
                    kingId = (sbyte)idData;

                    try
                    {
                        Encoding big5 = Encoding.GetEncoding("big5");
                        kingName = big5.GetString(nameData);
                    }
                    catch (DecoderFallbackException)
                    {
                        kingName = "【pk2.2】";
                    }
                }
            }
            catch (IOException)
            {
                kingName = "";
            }

            s_logger.Info($"{player.Name}上传存档.(君主:{kingName})");
            _ = RoomEventDispatcher.SendToRoom(roomId, EventTypes.SaveUploaded,
                new SaveUploadedEventData(player.ToDTO(), kingId, kingName));
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
