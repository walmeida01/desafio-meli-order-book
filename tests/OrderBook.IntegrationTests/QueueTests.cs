using FluentAssertions;
using OrderBook.Application.Modules.Orders.SubmitOrder;
using OrderBook.Domain.Shared;
using OrderBook.Infrastructure.Postgres;
using Xunit;

namespace OrderBook.IntegrationTests;

public sealed class QueueTests
{
    private static QueuedOrder Command(int number) => new(new SubmitOrderCommand(Guid.NewGuid(), UserId.Create(Guid.NewGuid()), Side.BUY, BrlCents.Create(1), Quantity.Create(1), IdempotencyKey.Create($"queue-{number}"), new byte[32]));

    [Fact]
    public void Bounded_queue_rejects_only_after_capacity_and_preserves_order()
    {
        var queue = new OrderCommandQueue();
        var commands = Enumerable.Range(0, 4096).Select(Command).ToArray();
        commands.All(queue.TryWrite).Should().BeTrue();
        queue.Count.Should().Be(4096);
        queue.TryWrite(Command(4097)).Should().BeFalse();
        queue.Count.Should().Be(4096);
        queue.StopProcessing(new InvalidOperationException("test drain"));
        commands.All(x => x.Completion.Task.IsFaulted).Should().BeTrue();
    }
}
