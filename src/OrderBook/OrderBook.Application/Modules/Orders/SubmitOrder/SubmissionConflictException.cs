namespace OrderBook.Application.Modules.Orders.SubmitOrder;

public sealed class SubmissionConflictException(string message) : Exception(message);
