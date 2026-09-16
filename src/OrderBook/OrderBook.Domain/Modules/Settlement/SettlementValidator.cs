using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Settlement;

public static class SettlementValidator
{
    public static void Validate(SettlementCommand command)
    {
        if (command.Fills.Count > 1000) throw new DomainException("Maximum fills per command exceeded.");
        if (command.Fills.Any(f => f.Quantity <= 0 || f.Price.Value <= 0)) throw new DomainException("Invalid settlement fill.");
    }
    public static IReadOnlyList<LedgerEntry> CreateLedger(Trade trade)
    {
        var brl = BrlCents.Multiply(trade.Quantity, trade.Price).Value;
        return [
            new(Guid.NewGuid(), trade.Id, LedgerEffectType.BUYER_BRL_DEBIT, trade.BuyerUserId, "BRL", "DEBIT", brl),
            new(Guid.NewGuid(), trade.Id, LedgerEffectType.BUYER_VIBRANIUM_CREDIT, trade.BuyerUserId, "VIBRANIUM", "CREDIT", trade.Quantity),
            new(Guid.NewGuid(), trade.Id, LedgerEffectType.SELLER_VIBRANIUM_DEBIT, trade.SellerUserId, "VIBRANIUM", "DEBIT", trade.Quantity),
            new(Guid.NewGuid(), trade.Id, LedgerEffectType.SELLER_BRL_CREDIT, trade.SellerUserId, "BRL", "CREDIT", brl)
        ];
    }
    public static DuplicateTradeDisposition CompareTrade(Trade expected, Trade persisted, IReadOnlyList<LedgerEntry> persistedLedgerEntries)
    {
        var expectedEffects = CreateLedger(expected);
        var complete = expected == persisted
            && persistedLedgerEntries.Count == expectedEffects.Count
            && persistedLedgerEntries.Select(entry => entry.EffectType).Distinct().Count() == expectedEffects.Count
            && expectedEffects.All(expectedEffect => persistedLedgerEntries.Any(actual => expectedEffect.TradeId == actual.TradeId && expectedEffect.EffectType == actual.EffectType && expectedEffect.UserId == actual.UserId && expectedEffect.Asset == actual.Asset && expectedEffect.Direction == actual.Direction && expectedEffect.Amount == actual.Amount));
        if (complete) return DuplicateTradeDisposition.IdenticalComplete;
        return expected.Id == persisted.Id ? DuplicateTradeDisposition.Conflicting : DuplicateTradeDisposition.New;
    }
    public static DuplicateTradeDisposition CompareTrade(Trade expected, Trade persisted, int persistedLedgerEntries) => expected == persisted && persistedLedgerEntries == 4 ? DuplicateTradeDisposition.IdenticalComplete : expected.Id == persisted.Id ? DuplicateTradeDisposition.Conflicting : DuplicateTradeDisposition.New;
}
