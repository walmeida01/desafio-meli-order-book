namespace OrderBook.FunctionalTests.Contracts;

public sealed record ReservationRecord(Guid OrderId, Guid UserId, string Side, string Asset, long OriginalAmount, long RemainingAmount, string Status);
