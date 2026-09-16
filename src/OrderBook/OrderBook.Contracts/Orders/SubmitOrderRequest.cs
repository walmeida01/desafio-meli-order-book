using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OrderBook.Contracts.Orders;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitOrderRequest
{
    public SubmitOrderRequest(Guid userId, string side, long priceBrlCents, long quantity)
    {
        UserId = userId;
        Side = side;
        PriceBrlCents = priceBrlCents;
        Quantity = quantity;
    }

    [Required]
    public Guid UserId { get; init; }

    [Required, RegularExpression("^(BUY|SELL)$")]
    public string Side { get; init; }

    [Range(1, long.MaxValue)]
    public long PriceBrlCents { get; init; }

    [Range(1, 1_000_000_000)]
    public long Quantity { get; init; }
}
