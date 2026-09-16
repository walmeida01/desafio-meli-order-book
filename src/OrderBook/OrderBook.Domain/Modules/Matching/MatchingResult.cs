namespace OrderBook.Domain.Modules.Matching;

public sealed record MatchingResult(IReadOnlyList<Fill> Fills, long ResidualQuantity);
