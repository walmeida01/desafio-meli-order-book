using Npgsql;
using OrderBook.Application.Ports;
using OrderBook.Domain.Shared;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresOrderSequence(PostgresUnitOfWork unitOfWork) : IOrderSequence
{
    public async Task<AcceptedSequence> NextAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.Connection is null || unitOfWork.Transaction is null)
            throw new InvalidOperationException("The order sequence must be consumed inside the command transaction.");
        await using var command = new NpgsqlCommand("SELECT nextval('accepted_order_sequence')", unitOfWork.Connection, unitOfWork.Transaction);
        var value = (long)(await command.ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("Sequence returned no value."));
        return AcceptedSequence.Create(value);
    }
}
