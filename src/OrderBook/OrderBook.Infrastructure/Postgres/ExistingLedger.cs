namespace OrderBook.Infrastructure.Postgres;

internal sealed class ExistingLedger
{
    public Guid TradeId { get; init; }
    public string EffectType { get; init; } = "";
    public Guid UserId { get; init; }
    public string Asset { get; init; } = "";
    public string Direction { get; init; } = "";
    public long Amount { get; init; }
}
