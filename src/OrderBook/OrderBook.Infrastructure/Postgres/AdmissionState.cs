namespace OrderBook.Infrastructure.Postgres;

public sealed class AdmissionState
{
    private int accepting;
    public bool Accepting => Volatile.Read(ref accepting) == 1;
    public void Start() => Interlocked.Exchange(ref accepting, 1);
    public void Stop() => Interlocked.Exchange(ref accepting, 0);
}
