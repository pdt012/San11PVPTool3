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
    [RequestSizeLimit(SaveManager.MaxRequestBodySizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = SaveManager.MaxRequestBodySizeBytes)]
    public async Task<IActionResult> Upload(
        [FromForm] string roomId,
        [FromForm] List<IFormFile> files)
    {
        var player = PlayerSessionAuth.Authenticate(Request, roomId);
        if (player == null)
            return Unauthorized();

        if (files.Count is < 1 or > SaveManager.MaxUploadFileCount)
            return BadRequest($"Upload must contain 1-{SaveManager.MaxUploadFileCount} save files.");

        var filesToSave = new List<(IFormFile File, string Path)>();
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalSize = 0;
        foreach (var file in files)
        {
            if (!SaveManager.TryGetSavePath(roomId, file.FileName, out var path))
                return BadRequest("Invalid save file name.");
            if (!fileNames.Add(file.FileName))
                return BadRequest("Duplicate save file name.");
            if (file.Length <= 0 || file.Length > SaveManager.MaxUploadFileSizeBytes)
                return BadRequest($"Each save file must be 1-{SaveManager.MaxUploadFileSizeBytes} bytes.");
            if (totalSize > SaveManager.MaxTotalUploadSizeBytes - file.Length)
                return BadRequest($"Total upload size exceeds {SaveManager.MaxTotalUploadSizeBytes} bytes.");

            totalSize += file.Length;
            filesToSave.Add((file, path));
        }

        var mainSave = filesToSave.FirstOrDefault(item =>
            string.Equals(item.File.FileName, SaveManager.DefaultFileName,
                StringComparison.OrdinalIgnoreCase));
        if (mainSave.File == null)
            return BadRequest($"Upload must contain {SaveManager.DefaultFileName}.");

        SaveDataSummary? saveDataSummary;
        try
        {
            await using var saveStream = mainSave.File.OpenReadStream();
            saveDataSummary = SaveDataParser.LoadSaveDataHeader(saveStream);
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or
                                   ArgumentException or IOException)
        {
            s_logger.Warn($"Rejected invalid save data: {ex.Message}");
            return BadRequest("Invalid save data.");
        }

        var lockObj = SaveManager.GetLock(roomId);
        await lockObj.WaitAsync();

        try
        {
            foreach (var (file, path) in filesToSave)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                await using var stream = System.IO.File.Create(path);
                await file.CopyToAsync(stream);
            }

            s_logger.Info($"{player.Name}上传存档.(君主:{
                saveDataSummary?.CurrentKingName ?? "??"} -> {saveDataSummary?.NextPlayerKingName ?? "??"})");
            await RoomEventDispatcher.SendToRoom(roomId, EventTypes.SaveUploaded,
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
        [FromQuery] string roomId,
        [FromQuery] string filename)
    {
        var player = PlayerSessionAuth.Authenticate(Request, roomId);
        if (player == null)
            return Unauthorized();

        if (!SaveManager.TryGetSavePath(roomId, filename, out var savePath))
            return BadRequest("Invalid save file name.");

        var baseSavePath = Path.Combine(_env.ContentRootPath,
            savePath);

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
        [FromQuery] string roomId,
        [FromQuery] string filename)
    {
        var player = PlayerSessionAuth.Authenticate(Request, roomId);
        if (player == null)
            return Unauthorized();

        if (!SaveManager.TryGetSavePath(roomId, filename, out var savePath))
            return BadRequest("Invalid save file name.");

        var lockObj = SaveManager.GetLock(roomId);
        await lockObj.WaitAsync();

        try
        {
            var path = Path.Combine(_env.ContentRootPath,
                savePath);

            if (!System.IO.File.Exists(path))
                return NotFound();

            s_logger.Info($"{player.Name}下载存档 {filename}");
            if (filename == SaveManager.DefaultFileName)
            {
                await RoomEventDispatcher.SendToRoom(roomId, EventTypes.SystemMessage,
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
