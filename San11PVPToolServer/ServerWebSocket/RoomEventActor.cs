using System.Threading.Channels;
using NLog;

namespace San11PVPToolServer.ServerWebSocket;

public class RoomEventActor
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly Channel<Func<Task>> _queue = Channel.CreateUnbounded<Func<Task>>();

    public RoomEventActor()
    {
        _ = ProcessLoop();
    }

    public async Task Enqueue(Func<Task> action)
    {
        await _queue.Writer.WriteAsync(action);
    }

    public Task<T> Enqueue<T>(Func<Task<T>> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Writer.TryWrite(async () =>
        {
            try
            {
                var result = await action();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private async Task ProcessLoop()
    {
        await foreach (var action in _queue.Reader.ReadAllAsync())
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                logger.Error($"EventActor Error: {ex}");
            }
        }
    }
}
