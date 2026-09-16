namespace OrderBook.Application.Modules.Orders.SubmitOrder;

public sealed class SubmissionUnavailableException(string message) : Exception(message);
