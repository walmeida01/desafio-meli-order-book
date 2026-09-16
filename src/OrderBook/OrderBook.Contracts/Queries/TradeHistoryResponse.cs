using System.ComponentModel.DataAnnotations;

namespace OrderBook.Contracts.Queries;

public sealed record TradeHistoryResponse(Guid TradeId, Guid TakerOrderId, Guid MakerOrderId, [property: Range(1, long.MaxValue)] long Quantity, [property: Range(1, long.MaxValue)] long PriceBrlCents, [property: Range(1, long.MaxValue)] long AcceptedSequence, [property: Range(1, int.MaxValue)] int FillOrdinal);
