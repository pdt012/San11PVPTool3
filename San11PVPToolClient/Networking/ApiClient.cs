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

    public async Task<RoomInfo> GetRoomInfo(string sessionToken, string roomId, CancellationToken token)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/room/info?roomId={Uri.EscapeDataString(roomId)}",
            sessionToken);
        using var response = await _http.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RoomInfo>(cancellationToken: token)
               ?? throw new InvalidDataException("Server returned an empty room response.");
    }

    public async Task<List<RoomInfoSummary>> GetRoomList(CancellationToken token)
    {
        return await _http.GetFromJsonAsync<List<RoomInfoSummary>>("/room/list", token) ?? [];
    }

    public async Task<CreateRoomResponse> CreateRoom(string playerName, RoomConfig config,
        CancellationToken token)
    {
        var req = new CreateRoomRequest(playerName, config);
        using var res = await _http.PostAsJsonAsync("/room/create", req, token);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CreateRoomResponse>(cancellationToken: token)
               ?? throw new InvalidDataException("Server returned an empty create-room response.");
    }

    public async Task<JoinRoomResponse> JoinRoom(string name, string roomId, string? password,
        CancellationToken token)
    {
        var req = new JoinRoomRequest(name, roomId, password);
        using var res = await _http.PostAsJsonAsync("/room/join", req, token);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JoinRoomResponse>(cancellationToken: token)
               ?? throw new InvalidDataException("Server returned an empty join-room response.");
    }

    public Task LeaveRoom(string sessionToken, string roomId) =>
        PostAuthenticated("/room/leave", new LeaveRoomRequest(roomId), sessionToken);

    public Task CloseRoom(string sessionToken, string roomId) =>
        PostAuthenticated("/room/close", new CloseRoomRequest(roomId), sessionToken);

    public Task KickPlayer(string sessionToken, string roomId, string targetPlayerId) =>
        PostAuthenticated("/room/kick", new KickPlayerRequest(roomId, targetPlayerId), sessionToken);

    public Task SetOwner(string sessionToken, string roomId, string targetPlayerId) =>
        PostAuthenticated("/room/set-owner", new SetOwnerRequest(roomId, targetPlayerId), sessionToken);

    public Task SetKingName(string sessionToken, string roomId, string targetPlayerId, string kingName) =>
        PostAuthenticated("/room/set-king-name", new SetKingNameRequest(roomId, targetPlayerId, kingName),
            sessionToken);

    public Task SetRoomConfig(string sessionToken, string roomId, RoomConfig config) =>
        PostAuthenticated("/room/set-config", new SetRoomConfigRequest(roomId, config), sessionToken);

    public async Task UploadSaveAsync(string sessionToken, string roomId, IList<string> filePaths)
    {
        using var form = new MultipartFormDataContent();
        foreach (var filePath in filePaths)
        {
            var stream = File.OpenRead(filePath);
            var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(content, "files", Path.GetFileName(filePath));
        }

        form.Add(new StringContent(roomId), "roomId");
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, "/save/upload", sessionToken);
        request.Content = form;
        using var res = await _http.SendAsync(request);

        switch (res.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new Exception("Upload failed: Your session is invalid or expired.");
            case HttpStatusCode.NotFound:
                throw new Exception("Upload failed: Room not found.");
            default:
                res.EnsureSuccessStatusCode();
                break;
        }
    }

    public async Task<List<string>> GetSaveListAsync(string sessionToken, string roomId, string filename)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/save/list?roomId={Uri.EscapeDataString(roomId)}&filename={Uri.EscapeDataString(filename)}",
            sessionToken);
        using var res = await _http.SendAsync(request);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<List<string>>() ?? [];
    }

    public async Task DownloadSaveAsync(string sessionToken, string roomId, string filename, string savePath)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/save/download?roomId={Uri.EscapeDataString(roomId)}&filename={Uri.EscapeDataString(filename)}",
            sessionToken);
        using var res = await _http.SendAsync(request);

        switch (res.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                throw new Exception("Download failed: Your session is invalid or expired.");
            case HttpStatusCode.NotFound:
                throw new Exception("Download failed: Save file or room not found.");
        }

        res.EnsureSuccessStatusCode();
        await using var stream = await res.Content.ReadAsStreamAsync();
        await using var file = File.Create(savePath);
        await stream.CopyToAsync(file);
    }

    private async Task PostAuthenticated<T>(string url, T body, string sessionToken)
    {
        using var request = CreateAuthenticatedRequest(HttpMethod.Post, url, sessionToken);
        request.Content = JsonContent.Create(body);
        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string url,
        string sessionToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        return request;
    }
}
