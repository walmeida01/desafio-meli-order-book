using Dapper;
using OrderBook.Application.Ports;
using OrderBook.Domain.Modules.Settlement;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresTradeRepository(PostgresUnitOfWork unitOfWork) : ITradeRepository
{
    public async Task SaveAsync(Trade trade, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for trade persistence.");
        await unitOfWork.Connection!.ExecuteAsync(new CommandDefinition("INSERT INTO trades(id,taker_order_id,maker_order_id,buyer_user_id,seller_user_id,quantity,price_brl_cents,accepted_sequence,fill_ordinal) VALUES(@id,@taker,@maker,@buyer,@seller,@quantity,@price,@sequence,@ordinal)", new { id = trade.Id.Value, taker = trade.TakerOrderId.Value, maker = trade.MakerOrderId.Value, buyer = trade.BuyerUserId.Value, seller = trade.SellerUserId.Value, trade.Quantity, price = trade.Price.Value, sequence = trade.AcceptedSequence.Value, ordinal = trade.FillOrdinal }, unitOfWork.Transaction, cancellationToken: cancellationToken));
    }
}
