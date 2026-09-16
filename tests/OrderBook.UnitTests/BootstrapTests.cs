using FluentAssertions;
using Xunit;

namespace OrderBook.UnitTests;

public sealed class BootstrapTests
{
    [Fact]
    public void Bootstrap_has_no_domain_behavior()
    {
        // AAA: the bootstrap domain boundary is intentionally only a marker in U00.
        var boundary = typeof(OrderBook.Domain.Shared.DomainBoundary);
        boundary.Should().NotBeNull();
    }
}
