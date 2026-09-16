using System.Net;
using FluentAssertions;
using OrderBook.FunctionalTests.Contracts;
using OrderBook.FunctionalTests.Features.Steps;
using OrderBook.FunctionalTests.Fixtures;
using Xunit;

namespace OrderBook.FunctionalTests.Features;

[Collection("API real collection")]
public sealed class OrderBookFunctionalTests(ApiCollectionFixture fixture)
{
    private static readonly Guid Buyer = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Seller = new("00000000-0000-0000-0000-000000000002");

    [Fact(DisplayName = "BDD-01 — reserva BUY")]
    public async Task Buy_reserves_brl()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Buyer, "BUY", 1_000, 10));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        steps.LastOrder!.Status.Should().Be("OPEN");
        var wallet = await steps.ReadWalletAsync(Buyer);
        wallet.BrlAvailable.Should().Be(99_990_000);
        wallet.BrlLocked.Should().Be(10_000);
    }

    [Fact(DisplayName = "BDD-02 — reserva SELL")]
    public async Task Sell_reserves_vibranium()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 10));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        var wallet = await steps.ReadWalletAsync(Seller);
        wallet.VibraniumAvailable.Should().Be(99_990);
        wallet.VibraniumLocked.Should().Be(10);
    }

    [Fact(DisplayName = "BDD-03 — saldo insuficiente")]
    public async Task Insufficient_balance_persists_rejected_order_without_effects()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Buyer, "BUY", 1_000, 200_000));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        steps.LastOrder!.Status.Should().Be("REJECTED");
        steps.LastOrder.AcceptedSequence.Should().BeNull();
        (await steps.ReadRejectedOrderCountAsync(steps.LastOrder.OrderId)).Should().Be(1);
        (await steps.ReadCountsAsync()).Should().Be(new DatabaseCounts(1, 0, 0, 0));
    }

    [Fact]
    public async Task Repeated_orders_are_distinct_submissions()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 3));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        var first = steps.LastOrder!;

        await steps.WhenSendAsync(new(Seller, "SELL", 900, 7));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        steps.LastOrder!.OrderId.Should().NotBe(first.OrderId);
        (await steps.ReadCountsAsync()).Orders.Should().Be(2);
        (await steps.ReadWalletAsync(Seller)).VibraniumLocked.Should().Be(10);
    }

    [Fact(DisplayName = "BDD-06 — maker price")]
    public async Task Compatible_orders_trade_at_maker_price()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 10));
        await steps.WhenSendAsync(new(Buyer, "BUY", 1_000, 10));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        steps.LastOrder!.Trades.Should().ContainSingle().Which.PriceBrlCents.Should().Be(900);
        (await steps.ReadTradesAsync()).Items.Should().ContainSingle().Which.PriceBrlCents.Should().Be(900);
    }

    [Fact(DisplayName = "BDD-07 — FIFO price-time")]
    public async Task Same_price_makers_are_consumed_in_acceptance_order()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 5));
        var firstMaker = steps.LastOrder!.OrderId;
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 5));
        var secondMaker = steps.LastOrder!.OrderId;
        await steps.WhenSendAsync(new(Buyer, "BUY", 900, 8));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        steps.LastOrder!.Status.Should().Be("FILLED");
        var trades = await steps.ReadTradesAsync();
        trades.Items.Should().HaveCount(2);
        trades.Items.Select(t => t.MakerOrderId).Should().ContainInOrder(firstMaker, secondMaker);
        trades.Items.Select(t => t.Quantity).Should().ContainInOrder(5, 3);
        (await steps.ReadOrderAsync(firstMaker)).Status.Should().Be("FILLED");
        (await steps.ReadOrderAsync(secondMaker)).Status.Should().Be("PARTIALLY_FILLED");
    }

    [Fact(DisplayName = "BDD-08 — partial fill")]
    public async Task Partial_fill_keeps_remaining_order_open()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 5));
        await steps.WhenSendAsync(new(Buyer, "BUY", 900, 10));
        steps.LastOrder!.Status.Should().Be("PARTIALLY_FILLED");
        steps.LastOrder.RemainingQuantity.Should().Be(5);
        var book = await steps.ReadOrderAsync(steps.LastOrder.OrderId);
        book.ExecutedQuantity.Should().Be(5);
        (await steps.ReadWalletAsync(Buyer)).BrlLocked.Should().Be(4_500);
    }

    [Fact(DisplayName = "BDD-09 — multiple fills")]
    public async Task Taker_consumes_multiple_makers_in_deterministic_order()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 2));
        await steps.WhenSendAsync(new(Seller, "SELL", 950, 3));
        await steps.WhenSendAsync(new(Buyer, "BUY", 1_000, 5));
        steps.LastOrder!.Trades.Select(t => t.Quantity).Should().ContainInOrder(2, 3);
        (await steps.ReadTradesAsync()).Items.Should().HaveCount(2);
        foreach (var trade in steps.LastOrder.Trades)
            (await steps.ReadLedgerCountForTradeAsync(trade.TradeId)).Should().Be(4);
    }

    [Fact(DisplayName = "BDD-10 — self-trade permitido")]
    public async Task Same_user_can_buy_and_sell_against_its_own_order()
    {
        var steps = await GivenAsync();
        await steps.WhenSendAsync(new(Buyer, "SELL", 900, 4));
        await steps.WhenSendAsync(new(Buyer, "BUY", 900, 4));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        steps.LastOrder!.Trades.Should().ContainSingle();
        (await steps.ReadLedgerCountForTradeAsync(steps.LastOrder.Trades[0].TradeId)).Should().Be(4);
    }

    [Fact(DisplayName = "BDD-11 — settlement e conservação")]
    public async Task Settlement_conserves_brl_and_vibranium_and_is_auditable()
    {
        var steps = await GivenAsync();
        var buyerBefore = await steps.ReadWalletAsync(Buyer);
        var sellerBefore = await steps.ReadWalletAsync(Seller);
        await steps.WhenSendAsync(new(Seller, "SELL", 900, 10));
        var makerOrderId = steps.LastOrder!.OrderId;
        await steps.WhenSendAsync(new(Buyer, "BUY", 1_000, 10));
        steps.LastResponse!.StatusCode.Should().Be(HttpStatusCode.Created);
        steps.LastOrder!.Status.Should().Be("FILLED");
        var trade = steps.LastOrder!.Trades.Should().ContainSingle().Which;
        var buyerAfter = await steps.ReadWalletAsync(Buyer);
        var sellerAfter = await steps.ReadWalletAsync(Seller);
        (buyerBefore.BrlAvailable + sellerBefore.BrlAvailable).Should().Be(buyerAfter.BrlAvailable + sellerAfter.BrlAvailable);
        (buyerBefore.VibraniumAvailable + sellerBefore.VibraniumAvailable).Should().Be(buyerAfter.VibraniumAvailable + sellerAfter.VibraniumAvailable);
        (buyerBefore.BrlAvailable + buyerBefore.BrlLocked + sellerBefore.BrlAvailable + sellerBefore.BrlLocked)
            .Should().Be(buyerAfter.BrlAvailable + buyerAfter.BrlLocked + sellerAfter.BrlAvailable + sellerAfter.BrlLocked);
        (buyerBefore.VibraniumAvailable + buyerBefore.VibraniumLocked + sellerBefore.VibraniumAvailable + sellerBefore.VibraniumLocked)
            .Should().Be(buyerAfter.VibraniumAvailable + buyerAfter.VibraniumLocked + sellerAfter.VibraniumAvailable + sellerAfter.VibraniumLocked);
        (buyerAfter.BrlLocked + sellerAfter.BrlLocked + buyerAfter.VibraniumLocked + sellerAfter.VibraniumLocked).Should().Be(0);
        var makerReservation = await steps.ReadReservationAsync(makerOrderId);
        makerReservation.Should().NotBeNull();
        makerReservation!.Status.Should().Be("RELEASED");
        makerReservation.RemainingAmount.Should().Be(0);
        var takerReservation = await steps.ReadReservationAsync(steps.LastOrder.OrderId);
        takerReservation.Should().NotBeNull();
        takerReservation!.Status.Should().Be("RELEASED");
        takerReservation.RemainingAmount.Should().Be(0);
        var ledger = await steps.ReadLedgerEntriesForTradeAsync(trade.TradeId);
        ledger.Should().BeEquivalentTo(new[]
        {
            new LedgerEntryRecord("BUYER_BRL_DEBIT", Buyer, "BRL", "DEBIT", 9_000),
            new LedgerEntryRecord("BUYER_VIBRANIUM_CREDIT", Buyer, "VIBRANIUM", "CREDIT", 10),
            new LedgerEntryRecord("SELLER_VIBRANIUM_DEBIT", Seller, "VIBRANIUM", "DEBIT", 10),
            new LedgerEntryRecord("SELLER_BRL_CREDIT", Seller, "BRL", "CREDIT", 9_000)
        }, options => options.WithoutStrictOrdering());
    }

    private async Task<OrderApiSteps> GivenAsync()
    {
        var steps = new OrderApiSteps(fixture);
        await steps.GivenCleanScenarioAsync();
        return steps;
    }
}
