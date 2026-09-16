using OrderBook.Domain.Shared;

namespace OrderBook.Application.Ports;

public interface IOrderSequence { Task<AcceptedSequence> NextAsync(CancellationToken cancellationToken); }
