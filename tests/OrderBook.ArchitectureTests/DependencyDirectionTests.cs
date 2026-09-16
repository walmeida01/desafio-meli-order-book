using FluentAssertions;
using System.Reflection;
using Xunit;

namespace OrderBook.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Domain_does_not_reference_outer_adapters()
    {
        var references = typeof(OrderBook.Domain.Shared.DomainBoundary).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name).ToArray();
        references.Should().NotContain("OrderBook.Infrastructure");
        references.Should().NotContain("OrderBook.Api");
        references.Should().NotContain("Dapper");
        references.Should().NotContain("Npgsql");
    }
}
