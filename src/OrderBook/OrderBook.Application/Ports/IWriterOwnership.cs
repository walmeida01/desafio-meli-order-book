namespace OrderBook.Application.Ports;

public interface IWriterOwnership { bool IsHeld { get; } Task<bool> AcquireAsync(CancellationToken cancellationToken); Task ReleaseAsync(CancellationToken cancellationToken); }
