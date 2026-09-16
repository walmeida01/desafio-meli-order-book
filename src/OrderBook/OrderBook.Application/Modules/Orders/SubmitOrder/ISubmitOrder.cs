namespace OrderBook.Application.Modules.Orders.SubmitOrder;

public interface ISubmitOrder
{
    bool TrySubmit(SubmitOrderCommand command, out Task<SubmitOrderResult> result);
}
