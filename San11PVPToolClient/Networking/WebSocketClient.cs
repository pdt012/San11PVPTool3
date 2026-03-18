using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using San11PVPToolClient.Events;
using San11PVPToolShared.Events;

namespace San11PVPToolClient.Networking;

public class WebSocketClient
{
    private ClientWebSocket? _socket;

    private readonly ClientEvents _events;

    private CancellationTokenSource? _cts;

    private string? _url;

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private Task? _receiveTask;

    private Task? _heartbeatTask;

    public bool IsConnected =>
        _socket != null &&
        _socket.State == WebSocketState.Open;

    public WebSocketClient(ClientEvents events)
    {
        _events = events;
    }

    public async Task Connect(string url, CancellationToken? token)
    {
        _url = url;

        await ConnectInternal(token);
    }

    private async Task ConnectInternal(CancellationToken? token)
    {
        _socket = new ClientWebSocket();
        _cts = new CancellationTokenSource();
        
        CancellationToken tokenNotNull;
        if (token == null)
        {
            var tempCts = new CancellationTokenSource();
            tokenNotNull = tempCts.Token;
        }
        else
        {
            tokenNotNull = token.Value;
        }

        await _socket.ConnectAsync(new Uri(_url!), tokenNotNull);
        _events.OnSocketConnected();

        _receiveTask = Task.Run(ReceiveLoop);
        _heartbeatTask = Task.Run(HeartbeatLoop);
    }

    async Task ReceiveLoop()
    {
        var buffer = new byte[8192];

        try
        {
            while (IsConnected)
            {
                var result = await _socket!.ReceiveAsync(
                    buffer,
                    _cts!.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                var json = Encoding.UTF8.GetString(
                    buffer,
                    0,
                    result.Count);
                try
                {
                    EventParser.Parse(json, _events);
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch (Exception ex)
        {
                    var a = ex.ToString();
            // ignored
        }
        finally
        {
            await HandleDisconnect();
        }
    }

    private async Task HeartbeatLoop()
    {
        try
        {
            while (IsConnected)
            {
                await Send(EventTypes.Ping, "ping");
                await Task.Delay(TimeSpan.FromSeconds(20), _cts!.Token);
            }
        }
        catch
        {
            // ignored
        }
    }

    public async Task Send(string eventType, object data)
    {
        if (!IsConnected)
            return;

        var json = JsonSerializer.Serialize(new SocketEvent(eventType, data));

        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync();

        try
        {
            await _socket!.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                _cts!.Token);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    async Task HandleDisconnect()
    {
        if (_cts == null)
            return;

        await _cts.CancelAsync();

        try
        {
            _socket?.Dispose();
        }
        catch
        {
            // ignored
        }

        _socket = null;

        _events.OnSocketDisconnected();
    }

    public async Task Disconnect()
    {
        try
        {
            await _cts?.CancelAsync();

            if (_socket != null &&
                _socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "disconnect",
                    CancellationToken.None);
            }
        }
        catch
        {
            // ignored
        }

        _socket?.Dispose();
        _socket = null;
    }
}
