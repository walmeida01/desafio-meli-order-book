using OrderBook.Application.Modules.Orders.SubmitOrder;

namespace OrderBook.Infrastructure.Postgres;

public sealed class QueuedOrder(SubmitOrderCommand command)
{
    public SubmitOrderCommand Command { get; } = command;
    public TaskCompletionSource<SubmitOrderResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
