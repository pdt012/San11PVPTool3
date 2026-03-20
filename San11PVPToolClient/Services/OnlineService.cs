using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using San11PVPToolClient.Events;
using San11PVPToolClient.Models;
using San11PVPToolClient.Networking;
using San11PVPToolShared.Events;
using San11PVPToolShared.Models;

namespace San11PVPToolClient.Services;

public class OnlineService
{
    public const string DefaultServerUrl = "http://localhost:5000";
    
    private ApiClient Api { set; get; }

    private WebSocketClient SocketClient { get; }

    public ClientEvents Events { get; } = new();

    public string ServerUrl
    {
        get;
        set
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                !(uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                  uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
               )
            {
                try
                {
                    uri = new Uri("http://" + value);
                }
                catch (UriFormatException)
                {
                    uri = new Uri(DefaultServerUrl);
                }
            }

            field = uri.AbsoluteUri;
            Api = new(uri);
        }
    }

    public bool IsConnected => SocketClient.IsConnected;

    /// <summary>
    /// 是否完全终止连接（不仅仅是掉线）
    /// </summary>
    public bool IsTerminated => _roomId == null || _playerId == null;

    private string? _roomId;
    private string? _playerId;


    #region NetworkConfig

    public const string NetworkConfigFileName = "NetworkConfig.json";
    
    public NetworkConfig NetworkConfig
    {
        get;
        set
        {
            field = value;
            Save();
        }
    }

    public void Save()
    {
        string json = JsonSerializer.Serialize(NetworkConfig);
        File.WriteAllText(NetworkConfigFileName, json);
    }

    private NetworkConfig Load()
    {
        if (!File.Exists(NetworkConfigFileName))
            return new NetworkConfig();

        string json = File.ReadAllText(NetworkConfigFileName);
        return JsonSerializer.Deserialize<NetworkConfig>(json)!;
    }

    #endregion

    public OnlineService()
    {
        NetworkConfig = Load();
        
        ServerUrl = NetworkConfig.ServerAddress;

        SocketClient = new WebSocketClient(Events);
    }

    public async Task<List<RoomInfoSummary>> GetRoomList(CancellationToken token)
    {
        return await Api.GetRoomList(token);
    }

    public async Task<(bool success, string message)> CreateRoom(string playerName, RoomConfig config,
        CancellationToken token)
    {
        var response = await Api.CreateRoom(playerName, _playerId, config, token);
        if (!response.Success)
            return (response.Success, response.Message);

        _roomId = response.RoomInfo.RoomId;
        _playerId = response.UserInfo.PlayerId;

        Events.OnLoginStateChanged(response.UserInfo, response.RoomInfo);

        await ConnectSocket();

        return (response.Success, response.Message);
    }

    public async Task<(bool success, string message)> JoinRoom(string roomId, string playerName,
        string? password, CancellationToken token)
    {
        var response = await Api.JoinRoom(roomId, _playerId, playerName, password, token);
        if (!response.Success)
            return (response.Success, response.Message);

        _roomId = response.RoomInfo.RoomId;
        _playerId = response.UserInfo.PlayerId;

        Events.OnLoginStateChanged(response.UserInfo, response.RoomInfo);

        await ConnectSocket();

        return (response.Success, response.Message);
    }

    public async Task LeaveRoom()
    {
        if (_roomId == null || _playerId == null) return;

        _ = Api.LeaveRoom(_playerId, _roomId);

        _ = TerminateSocket();
    }

    public async Task CloseRoom()
    {
        if (_roomId == null || _playerId == null) return;

        _ = Api.CloseRoom(_playerId, _roomId);

        _ = TerminateSocket();
    }

    public async Task KickPlayer(string targetPlayerId)
    {
        if (_roomId == null || _playerId == null) return;

        await Api.KickPlayer(_playerId, _roomId, targetPlayerId);
    }

    public async Task SetOwner(string targetPlayerId)
    {
        if (_roomId == null || _playerId == null) return;

        await Api.SetOwner(_playerId, _roomId, targetPlayerId);
    }

    public async Task SetKingName(string targetPlayerId, string kingName)
    {
        if (_roomId == null || _playerId == null) return;

        await Api.SetKingName(_playerId, _roomId, targetPlayerId, kingName);
    }

    public async Task SetRoomConfig(RoomConfig config)
    {
        if (_roomId == null || _playerId == null) return;

        await Api.SetRoomConfig(_playerId, _roomId, config);
    }

    public async Task SendMessage(string message)
    {
        await SocketClient.Send(EventTypes.ChatMessage, message);
    }

    public async Task UploadSave(IList<string> filePaths)
    {
        if (_roomId == null || _playerId == null) return;

        await Api.UploadSaveAsync(_playerId, _roomId, filePaths);
    }

    public async Task DownloadSave(string filePath)
    {
        if (_roomId == null || _playerId == null) return;

        var files = await Api.GetSaveListAsync(_playerId, _roomId, Path.GetFileName(filePath));
        // 先下载到临时文件
        foreach (var filename in files)
        {
            var tempPath = Path.ChangeExtension(filePath, Path.GetExtension(filename)) + ".tmp";
            await Api.DownloadSaveAsync(_playerId, _roomId, filename, tempPath);
        }
        // 统一重命名
        foreach (var filename in files)
        {
            var targetPath = Path.ChangeExtension(filePath, Path.GetExtension(filename));
            var tempPath = targetPath + ".tmp";
            File.Move(tempPath, targetPath, true);
        }
    }

    public async Task<RoomInfo?> GetRoomInfo(CancellationToken token)
    {
        if (_roomId == null || _playerId == null) return null;

        return await Api.GetRoomInfo(_roomId, token);
    }

    private async Task ConnectSocket(CancellationToken? token = null)
    {
        if (_roomId == null || _playerId == null)
            throw new Exception("Room not initialized or socket terminated.");

        var wsUrl = ServerUrl
            .TrimEnd('/')
            .Replace("http://", "ws://")
            .Replace("https://", "wss://");

        wsUrl += $"/ws?roomId={_roomId}&playerId={_playerId}";

        await SocketClient.Connect(wsUrl, token);
    }

    /// <summary>
    /// 彻底结束连接状态
    /// </summary>
    public async Task TerminateSocket()
    {
        _roomId = null;
        _playerId = null;
        Events.OnLoginStateChanged(null, null);
        await SocketClient.Disconnect();
    }

    public async Task Reconnect(CancellationToken token)
    {
        await ConnectSocket(token);
    }
}
