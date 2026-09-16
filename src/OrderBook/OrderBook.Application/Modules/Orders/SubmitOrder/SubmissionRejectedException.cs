namespace OrderBook.Application.Modules.Orders.SubmitOrder;

public sealed class SubmissionRejectedException(string message) : Exception(message);
