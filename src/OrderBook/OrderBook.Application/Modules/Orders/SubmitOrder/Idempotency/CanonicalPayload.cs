using System.Security.Cryptography;
using System.Text;

namespace OrderBook.Application.Modules.Orders.SubmitOrder.Idempotency;

public static class CanonicalPayload
{
    public static byte[] Serialize(CanonicalOrderPayload payload) => Encoding.UTF8.GetBytes($"{{\"userId\":\"{payload.UserId:D}\",\"side\":\"{payload.Side}\",\"priceBrlCents\":{payload.PriceBrlCents},\"quantity\":{payload.Quantity}}}");
    public static byte[] Hash(CanonicalOrderPayload payload) => SHA256.HashData(Serialize(payload));
}
