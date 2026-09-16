using System.Threading.Channels;

namespace OrderBook.Infrastructure.Postgres;

public sealed class OrderCommandQueue
{
    private readonly Channel<QueuedOrder> channel = Channel.CreateBounded<QueuedOrder>(new BoundedChannelOptions(4096) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly CancellationTokenSource processingCancellation = new();
    private int count;
    public bool TryWrite(QueuedOrder command)
    {
        if (!channel.Writer.TryWrite(command)) return false;
        Interlocked.Increment(ref count);
        return true;
    }
    public CancellationToken ProcessingCancellation => processingCancellation.Token;
    public int Count => Volatile.Read(ref count);
    public async IAsyncEnumerable<QueuedOrder> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var queued in channel.Reader.ReadAllAsync(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, processingCancellation.Token).Token))
        {
            Interlocked.Decrement(ref count);
            yield return queued;
        }
    }
    public void Complete() => channel.Writer.TryComplete();
    public void StopProcessing(Exception reason)
    {
        channel.Writer.TryComplete(reason);
        processingCancellation.Cancel();
        while (channel.Reader.TryRead(out var queued)) { Interlocked.Decrement(ref count); queued.Completion.TrySetException(reason); }
    }
}
