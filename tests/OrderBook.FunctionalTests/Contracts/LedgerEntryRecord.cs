namespace OrderBook.FunctionalTests.Contracts;

public sealed record LedgerEntryRecord(string EffectType, Guid UserId, string Asset, string Direction, long Amount);
