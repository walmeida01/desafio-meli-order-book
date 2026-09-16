using FluentAssertions;
using OrderBook.Domain.Modules.Settlement;
using OrderBook.Domain.Shared;
using Xunit;

namespace OrderBook.UnitTests;

public sealed class TradeDuplicateTests
{
    private static Trade Trade(long quantity = 2) => new(TradeId.Create(Guid.Parse("00000000-0000-0000-0000-000000000010")), OrderId.Create(Guid.Parse("00000000-0000-0000-0000-000000000011")), OrderId.Create(Guid.Parse("00000000-0000-0000-0000-000000000012")), UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000013")), UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000014")), quantity, BrlCents.Create(10), AcceptedSequence.Create(3), 1);

    [Fact]
    public void Identical_trade_with_four_effects_is_a_noop() => SettlementValidator.CompareTrade(Trade(), Trade(), SettlementValidator.CreateLedger(Trade())).Should().Be(DuplicateTradeDisposition.IdenticalComplete);

    [Fact]
    public void Identical_trade_without_complete_ledger_is_conflict() => SettlementValidator.CompareTrade(Trade(), Trade(), 3).Should().Be(DuplicateTradeDisposition.Conflicting);

    [Fact]
    public void Same_id_with_different_content_is_conflict() => SettlementValidator.CompareTrade(Trade(), Trade(3), 4).Should().Be(DuplicateTradeDisposition.Conflicting);

    [Fact]
    public void Four_effects_with_wrong_content_are_conflict()
    {
        var effects = SettlementValidator.CreateLedger(Trade()).ToArray();
        effects[0] = effects[0] with { Amount = effects[0].Amount + 1 };
        SettlementValidator.CompareTrade(Trade(), Trade(), effects).Should().Be(DuplicateTradeDisposition.Conflicting);
    }
}
