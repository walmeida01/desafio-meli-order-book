using OrderBook.Application.Ports;

namespace OrderBook.Infrastructure.Postgres;

public sealed class RuntimeReadiness : IReadiness
{
    private int ready;
    public bool IsReady => Volatile.Read(ref ready) == 1;
    public void MarkReady() => Interlocked.Exchange(ref ready, 1);
    public void MarkNotReady() => Interlocked.Exchange(ref ready, 0);
}
