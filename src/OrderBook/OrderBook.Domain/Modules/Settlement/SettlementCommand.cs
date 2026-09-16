using OrderBook.Domain.Modules.Matching;

namespace OrderBook.Domain.Modules.Settlement;

public sealed record SettlementCommand(IReadOnlyList<Fill> Fills);
