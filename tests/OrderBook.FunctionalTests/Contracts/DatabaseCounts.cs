namespace OrderBook.FunctionalTests.Contracts;

public sealed record DatabaseCounts(long Orders, long Reservations, long Trades, long LedgerEntries);
