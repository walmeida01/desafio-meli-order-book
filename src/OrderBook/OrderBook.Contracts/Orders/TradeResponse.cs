using System.ComponentModel.DataAnnotations;

namespace OrderBook.Contracts.Orders;

public sealed record TradeResponse(Guid TradeId, [property: Range(1, long.MaxValue)] long Quantity, [property: Range(1, long.MaxValue)] long PriceBrlCents);
