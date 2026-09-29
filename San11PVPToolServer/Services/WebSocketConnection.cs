using System.Net.WebSockets;

namespace San11PVPToolServer.Services;

public sealed class WebSocketConnection
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public Guid ConnectionId { get; } = Guid.NewGuid();
    public WebSocket Socket { get; }
    public WebSocketState State => Socket.State;

    public WebSocketConnection(WebSocket socket)
    {
        Socket = socket;
    }

    public async Task<bool> SendAsync(ReadOnlyMemory<byte> data, bool disconnectAfterSent = false)
    {
        await _sendLock.WaitAsync();
        try
        {
            if (Socket.State != WebSocketState.Open)
                return false;

            using var cts = new CancellationTokenSource(OperationTimeout);
            await Socket.SendAsync(data, WebSocketMessageType.Text, true, cts.Token);

            if (disconnectAfterSent && Socket.State == WebSocketState.Open)
            {
                await Socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Disconnected by server",
                    cts.Token);
            }

            return true;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task CloseOutputAsync(string description)
    {
        await _sendLock.WaitAsync();
        try
        {
            if (Socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
                return;

            using var cts = new CancellationTokenSource(OperationTimeout);
            await Socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, description, cts.Token);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Abort()
    {
        try
        {
            Socket.Abort();
        }
        catch
        {
            // ignored
        }
    }

    public async Task DisposeAsync()
    {
        await _sendLock.WaitAsync();
        try
        {
            Socket.Dispose();
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
