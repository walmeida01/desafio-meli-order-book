using FluentAssertions;
using OrderBook.Application.Modules.Orders.SubmitOrder.Idempotency;
using Xunit;

namespace OrderBook.UnitTests;

public sealed class SubmitOrderPayloadTests
{
    [Fact]
    public void Equivalent_payload_is_hashed_deterministically()
    {
        var payload = new CanonicalOrderPayload(Guid.Parse("00000000-0000-0000-0000-000000000001"), "BUY", 100, 2);
        CanonicalPayload.Hash(payload).Should().Equal(CanonicalPayload.Hash(payload));
    }

    [Fact]
    public void Canonical_payload_has_contract_property_order_and_no_whitespace()
    {
        var payload = new CanonicalOrderPayload(Guid.Parse("00000000-0000-0000-0000-000000000001"), "BUY", 100, 2);
        CanonicalPayload.Serialize(payload).Should().Equal(System.Text.Encoding.UTF8.GetBytes("{\"userId\":\"00000000-0000-0000-0000-000000000001\",\"side\":\"BUY\",\"priceBrlCents\":100,\"quantity\":2}"));
    }
}
