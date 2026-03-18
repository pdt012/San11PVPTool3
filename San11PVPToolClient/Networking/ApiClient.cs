using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Networking;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(Uri baseUri)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5), BaseAddress = baseUri };
    }

    public async Task<RoomInfo> GetRoomInfo(string roomId, CancellationToken token)
    {
        return await _http.GetFromJsonAsync<RoomInfo>($"/room/info?roomId={roomId}", token);
    }

    public async Task<List<RoomInfoSummary>> GetRoomList(CancellationToken token)
    {
        return await _http.GetFromJsonAsync<List<RoomInfoSummary>>("/room/list", token);
    }

    public async Task<CreateRoomResponse> CreateRoom(string playerName, string? playerId, RoomConfig config,
        CancellationToken token)
    {
        var req = new CreateRoomRequest(playerName, config);

        var res = await _http.PostAsJsonAsync("/room/create", req, token);

        res.EnsureSuccessStatusCode();

        var response = await res.Content.ReadFromJsonAsync<CreateRoomResponse>();

        return response;
    }

    public async Task<JoinRoomResponse> JoinRoom(string name, string? playerId, string roomId, string? password,
        CancellationToken token)
    {
        var req = new JoinRoomRequest(name, roomId, password);

        var res = await _http.PostAsJsonAsync("/room/join", req, token);

        res.EnsureSuccessStatusCode();

        var response = await res.Content.ReadFromJsonAsync<JoinRoomResponse>();

        return response;
    }

    public async Task LeaveRoom(string playerId, string roomId)
    {
        var req = new LeaveRoomRequest(playerId, roomId);

        await _http.PostAsJsonAsync("/room/leave", req);
    }

    public async Task CloseRoom(string playerId, string roomId)
    {
        var req = new CloseRoomRequest(playerId, roomId);

        await _http.PostAsJsonAsync("/room/close", req);
    }

    public async Task KickPlayer(string playerId, string roomId, string targetPlayerId)
    {
        var req = new KickPlayerRequest(playerId, roomId, targetPlayerId);

        await _http.PostAsJsonAsync("/room/kick", req);
    }

    public async Task SetOwner(string playerId, string roomId, string targetPlayerId)
    {
        var req = new SetOwnerRequest(playerId, roomId, targetPlayerId);

        await _http.PostAsJsonAsync("/room/set-owner", req);
    }

    public async Task SetKingName(string playerId, string roomId, string targetPlayerId, string kingName)
    {
        var req = new SetKingNameRequest(playerId, roomId, targetPlayerId, kingName);

        await _http.PostAsJsonAsync("/room/set-king-name", req);
    }

    public async Task SetRoomConfig(string playerId, string roomId, RoomConfig config)
    {
        var req = new SetRoomConfigRequest(playerId, roomId, config);

        await _http.PostAsJsonAsync("/room/set-config", req);
    }

    public async Task UploadSaveAsync(string playerId, string roomId, IList<string> filePaths)
    {
        using var form = new MultipartFormDataContent();
        foreach (var filePath in filePaths)
        {
            var stream = File.OpenRead(filePath);
            var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(content, "files", Path.GetFileName(filePath));
        }

        form.Add(new StringContent(playerId), "playerId");
        form.Add(new StringContent(roomId), "roomId");

        var res = await _http.PostAsync("/save/upload", form);

        switch (res.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new Exception("Upload failed: You do not have permission to upload in this room.");
            case HttpStatusCode.NotFound:
                throw new Exception("Upload failed: Room not found.");
            default:
                res.EnsureSuccessStatusCode();
                break;
        }
    }

    public async Task<List<string>> GetSaveListAsync(string playerId, string roomId, string filename)
    {
        var res = await _http.GetAsync(
            $"/save/list?playerId={playerId}&roomId={roomId}&filename={filename}");

        res.EnsureSuccessStatusCode();

        var list = await res.Content.ReadFromJsonAsync<List<string>>();

        return list ?? new List<string>();
    }

    public async Task DownloadSaveAsync(string playerId, string roomId, string filename, string savePath)
    {
        var res = await _http.GetAsync(
            $"/save/download?playerId={playerId}&roomId={roomId}&filename={filename}");

        switch (res.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new Exception("Download failed: You do not have permission to download from this room.");
            case HttpStatusCode.NotFound:
                throw new Exception("Download failed: Save file or room not found.");
        }

        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync();
        await using var file = File.Create(savePath);

        await stream.CopyToAsync(file);
    }
}
