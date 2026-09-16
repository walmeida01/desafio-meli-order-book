using FluentAssertions;
using OrderBook.Domain.Modules.Matching;
using OrderBook.Domain.Modules.Orders;
using OrderBook.Domain.Shared;
using Xunit;

namespace OrderBook.UnitTests;

public sealed class MatchingConcurrencyTests
{
    private static Order Open(Side side, long price, long quantity, long sequence, Guid? user = null)
    {
        var order = Order.Create(OrderId.Create(Guid.NewGuid()), UserId.Create(user ?? Guid.NewGuid()), side, BrlCents.Create(price), Quantity.Create(quantity));
        order.Accept(AcceptedSequence.Create(sequence));
        return order;
    }

    [Fact]
    public void Matching_supports_partial_and_multiple_fills_in_price_time_order()
    {
        var book = new global::OrderBook.Domain.Modules.Matching.OrderBook();
        var first = Open(Side.SELL, 100, 2, 1);
        var second = Open(Side.SELL, 110, 3, 2);
        book.Add(first); book.Add(second);
        var taker = Open(Side.BUY, 110, 4, 3);

        var result = new MatchingEngine(book).Match(taker);

        result.Fills.Select(x => x.Quantity).Should().Equal(2, 2);
        result.Fills.Select(x => x.Price.Value).Should().Equal(100, 110);
        result.ResidualQuantity.Should().Be(0);
        book.Snapshot(Side.SELL).Should().ContainSingle().Which.RemainingQuantity.Should().Be(1);
    }

    [Fact]
    public void Self_trade_is_not_filtered()
    {
        var user = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var book = new global::OrderBook.Domain.Modules.Matching.OrderBook();
        var maker = Open(Side.SELL, 100, 1, 1, user);
        book.Add(maker);
        var result = new MatchingEngine(book).Match(Open(Side.BUY, 100, 1, 2, user));
        result.Fills.Should().ContainSingle();
        result.Fills[0].MakerOrderId.Should().Be(maker.Id);
    }
}
