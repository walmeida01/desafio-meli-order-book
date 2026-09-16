using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Settlement;

public sealed record LedgerEntry(Guid Id, TradeId TradeId, LedgerEffectType EffectType, UserId UserId, string Asset, string Direction, long Amount);
