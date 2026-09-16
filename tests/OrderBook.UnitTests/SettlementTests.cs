using FluentAssertions;
using OrderBook.Domain.Modules.Matching;
using OrderBook.Domain.Modules.Settlement;
using OrderBook.Domain.Shared;
using Xunit;

namespace OrderBook.UnitTests;

public sealed class SettlementTests
{
    [Fact]
    public void A_fill_creates_exactly_four_ledger_effects()
    {
        var trade = new Trade(TradeId.Create(Guid.NewGuid()), OrderId.Create(Guid.NewGuid()), OrderId.Create(Guid.NewGuid()), UserId.Create(Guid.NewGuid()), UserId.Create(Guid.NewGuid()), 3, BrlCents.Create(25), AcceptedSequence.Create(1), 1);
        var effects = SettlementValidator.CreateLedger(trade);
        effects.Should().HaveCount(4);
        effects.Select(x => x.EffectType).Should().BeEquivalentTo(Enum.GetValues<LedgerEffectType>());
    }

    [Fact]
    public void Settlement_rejects_more_than_one_thousand_fills()
    {
        var fill = new Fill(TradeId.Create(Guid.NewGuid()), OrderId.Create(Guid.NewGuid()), OrderId.Create(Guid.NewGuid()), Side.BUY, 1, BrlCents.Create(1), 1);
        var command = new SettlementCommand(Enumerable.Repeat(fill, 1001).ToArray());
        var action = () => SettlementValidator.Validate(command);
        action.Should().Throw<DomainException>();
    }
}
