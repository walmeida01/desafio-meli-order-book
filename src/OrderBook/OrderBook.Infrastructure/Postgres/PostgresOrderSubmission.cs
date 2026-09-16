using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Diagnostics;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Logging;
using OrderBook.Application.Modules.Orders.SubmitOrder;
using OrderBook.Domain.Modules.Matching;
using OrderBook.Domain.Modules.Orders;
using OrderBook.Domain.Modules.Settlement;
using OrderBook.Domain.Shared;
using OrderBook.Infrastructure.Observability;
using OpenTelemetry.Trace;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresOrderSubmission(PostgresConnectionFactory factory, global::OrderBook.Domain.Modules.Matching.OrderBook book, ILogger<PostgresOrderSubmission> logger, OrderBookMetrics metrics, PostgresWriterOwnership ownership, OrderCommandQueue queue, System.Diagnostics.ActivitySource activitySource) : ISubmitOrder
{
    public bool TrySubmit(SubmitOrderCommand command, out Task<SubmitOrderResult> result)
    {
        var queued = new QueuedOrder(command); result = queued.Completion.Task;
        if (!Queue.TryWrite(queued)) { result = Task.FromException<SubmitOrderResult>(new SubmissionUnavailableException("Order queue is full.")); return false; }
        return true;
    }
    public OrderCommandQueue Queue { get; } = queue;
    public void StopProcessing() => Queue.StopProcessing(new SubmissionUnavailableException("Writer ownership was lost."));
    public async Task ProcessAsync(QueuedOrder queued, CancellationToken cancellationToken)
    {
        if (!await ownership.CheckAsync(cancellationToken))
            throw new SubmissionUnavailableException("Writer ownership is not held.");
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var output = await ExecuteMeasuredAsync(queued.Command, cancellationToken);
                metrics.OrdersProcessed.Add(1, new KeyValuePair<string, object?>("status", output.Replay ? "replayed" : "accepted"));
                if (!output.Replay) metrics.TradesExecuted.Add(output.Trades.Count);
                queued.Completion.TrySetResult(output);
                return;
            }
            catch (PostgresException ex) when (attempt < 3 && ex.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure) { logger.LogWarning("Retrying order transaction {Attempt}: {Error}", attempt, SensitiveDataRedactor.Redact(ex.Message)); }
            catch (PostgresException ex) when (attempt < 3 && ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "idempotency_records_pkey") { logger.LogWarning("Retrying idempotency race {Attempt}: {Error}", attempt, SensitiveDataRedactor.Redact(ex.Message)); }
            catch (SubmissionRejectedException exception)
            {
                try { var output = await PersistRejectionMeasuredAsync(queued.Command, exception.Message, cancellationToken); metrics.OrdersProcessed.Add(1, new KeyValuePair<string, object?>("status", output.Replay ? "replayed" : "rejected")); queued.Completion.TrySetResult(output); }
                catch (Exception persistenceFailure) { queued.Completion.TrySetException(persistenceFailure); }
                return;
            }
            catch (DomainException exception)
            {
                try { var output = await PersistRejectionMeasuredAsync(queued.Command, exception.Message, cancellationToken); metrics.OrdersProcessed.Add(1, new KeyValuePair<string, object?>("status", output.Replay ? "replayed" : "rejected")); queued.Completion.TrySetResult(output); }
                catch (Exception persistenceFailure) { queued.Completion.TrySetException(persistenceFailure); }
                return;
            }
            catch (OverflowException exception)
            {
                try { var output = await PersistRejectionMeasuredAsync(queued.Command, "Financial amount overflow.", cancellationToken); metrics.OrdersProcessed.Add(1, new KeyValuePair<string, object?>("status", output.Replay ? "replayed" : "rejected")); queued.Completion.TrySetResult(output); }
                catch (Exception persistenceFailure) { queued.Completion.TrySetException(persistenceFailure); }
                logger.LogWarning(exception, "Order rejected because a financial amount overflowed.");
                return;
            }
            catch (Exception ex) { metrics.OrdersProcessed.Add(1, new KeyValuePair<string, object?>("status", "failed")); logger.LogError("Order command failed: {Error}", SensitiveDataRedactor.Redact(ex.Message)); queued.Completion.TrySetException(ex); return; }
        }
    }
    private async Task<SubmitOrderResult> ExecuteMeasuredAsync(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        return await ExecuteAsync(command, cancellationToken);
    }

    private async Task<SubmitOrderResult> PersistRejectionMeasuredAsync(SubmitOrderCommand command, string reason, CancellationToken cancellationToken)
    {
        return await PersistRejectionAsync(command, reason, cancellationToken);
    }

    private async Task<SubmitOrderResult> ExecuteAsync(SubmitOrderCommand command, CancellationToken cancellationToken)
    {
        using var databaseActivity = activitySource.StartActivity("orderbook.db.transaction");
        await using var connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var transactionStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            await using var timeout = new NpgsqlCommand("SET LOCAL statement_timeout = 2000", connection, transaction); await timeout.ExecuteNonQueryAsync(cancellationToken);
            var existing = await connection.QuerySingleOrDefaultAsync<StoredIdempotency>(new CommandDefinition("SELECT response_status Status,response_body::text Body,canonical_payload_hash Hash FROM idempotency_records WHERE user_id=@user AND idempotency_key=@key FOR UPDATE", new { user = command.UserId.Value, key = command.IdempotencyKey.Value }, transaction, cancellationToken: cancellationToken));
            if (existing is not null)
            {
                if (!existing.Hash.AsSpan().SequenceEqual(command.CanonicalHash)) throw new SubmissionConflictException("Idempotency key was used with a different payload.");
                var replay = JsonSerializer.Deserialize<SubmitOrderResult>(existing.Body) ?? throw new InvalidOperationException("Persisted idempotency response is invalid.");
                await transaction.CommitAsync(cancellationToken);
                RecordTransactionMetrics(transactionStarted);
                return replay with { Replay = true, HttpStatus = 200 };
            }
            var sequence = (long)(await new NpgsqlCommand("SELECT nextval('accepted_order_sequence')", connection, transaction).ExecuteScalarAsync(cancellationToken) ?? throw new InvalidOperationException("Sequence failed."));
            var order = Order.Create(OrderId.Create(Guid.NewGuid()), command.UserId, command.Side, command.Price, command.Quantity); order.Accept(AcceptedSequence.Create(sequence));
            var matchingStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            using var matchingActivity = activitySource.StartActivity("orderbook.matching");
            var candidate = book.Copy();
            var knownMakers = candidate.Snapshot(Side.BUY).Concat(candidate.Snapshot(Side.SELL)).ToDictionary(x => x.Id);
            var matching = new MatchingEngine(candidate).Match(order);
            metrics.MatchingDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(matchingStarted).TotalSeconds);
            var involvedUsers = new HashSet<Guid> { command.UserId.Value };
            foreach (var fill in matching.Fills)
            {
                var maker = knownMakers[fill.MakerOrderId];
                involvedUsers.Add(fill.TakerSide == Side.BUY ? command.UserId.Value : maker.UserId.Value);
                involvedUsers.Add(fill.TakerSide == Side.SELL ? command.UserId.Value : maker.UserId.Value);
            }
            var users = involvedUsers.OrderBy(userId => userId).ToArray();
            var lockedUsers = (await connection.QueryAsync<Guid>(new CommandDefinition("SELECT user_id FROM wallets WHERE user_id = ANY(@users) ORDER BY user_id FOR UPDATE", new { users }, transaction, cancellationToken: cancellationToken))).ToArray();
            if (lockedUsers.Length != users.Length) throw new SubmissionRejectedException("A wallet required by the order does not exist.");
            var reserve = command.Side == Side.BUY ? checked(command.Quantity.Value * command.Price.Value) : command.Quantity.Value;
            var reserveSql = command.Side == Side.BUY ? "UPDATE wallets SET brl_available=brl_available-@amount,brl_locked=brl_locked+@amount,version=version+1,updated_at=now() WHERE user_id=@user AND brl_available>=@amount" : "UPDATE wallets SET vibranium_available=vibranium_available-@amount,vibranium_locked=vibranium_locked+@amount,version=version+1,updated_at=now() WHERE user_id=@user AND vibranium_available>=@amount";
            if (await connection.ExecuteAsync(new CommandDefinition(reserveSql, new { amount = reserve, user = command.UserId.Value }, transaction, cancellationToken: cancellationToken)) != 1) throw new SubmissionRejectedException("Insufficient wallet balance.");
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO orders(id,user_id,instrument,side,limit_price_brl_cents,original_quantity,remaining_quantity,status,accepted_sequence) VALUES(@id,@user,'VIBRANIUM',@side,@price,@original,@original,'OPEN',@sequence); INSERT INTO reservations(id,order_id,user_id,side,asset,original_amount,remaining_amount,status) VALUES(@reservation,@id,@user,@side,@asset,@amount,@amount,'ACTIVE')", new { id = order.Id.Value, reservation = Guid.NewGuid(), user = command.UserId.Value, side = command.Side.ToString(), asset = command.Side == Side.BUY ? "BRL" : "VIBRANIUM", price = command.Price.Value, original = command.Quantity.Value, sequence, amount = reserve }, transaction, cancellationToken: cancellationToken));
            var responses = new List<SubmittedTrade>();
            foreach (var fill in matching.Fills)
            {
                var maker = knownMakers[fill.MakerOrderId];
                var buyer = fill.TakerSide == Side.BUY ? command.UserId.Value : maker.UserId.Value; var seller = fill.TakerSide == Side.SELL ? command.UserId.Value : maker.UserId.Value;
                var expectedTrade = new Trade(fill.TradeId, fill.TakerOrderId, fill.MakerOrderId, UserId.Create(buyer), UserId.Create(seller), fill.Quantity, fill.Price, AcceptedSequence.Create(sequence), fill.FillOrdinal);
                var existingTrade = await connection.QuerySingleOrDefaultAsync<ExistingTrade>(new CommandDefinition("SELECT id TradeId,taker_order_id TakerOrderId,maker_order_id MakerOrderId,buyer_user_id BuyerUserId,seller_user_id SellerUserId,quantity Quantity,price_brl_cents Price,accepted_sequence AcceptedSequence,fill_ordinal FillOrdinal FROM trades WHERE id=@id FOR UPDATE", new { id = fill.TradeId.Value }, transaction, cancellationToken: cancellationToken));
                if (existingTrade is not null)
                {
                    var existingLedger = (await connection.QueryAsync<ExistingLedger>(new CommandDefinition("SELECT trade_id TradeId,effect_type EffectType,user_id UserId,asset Asset,direction Direction,amount Amount FROM ledger_entries WHERE trade_id=@id", new { id = fill.TradeId.Value }, transaction, cancellationToken: cancellationToken))).Select(entry => new LedgerEntry(Guid.NewGuid(), TradeId.Create(entry.TradeId), Enum.Parse<LedgerEffectType>(entry.EffectType), UserId.Create(entry.UserId), entry.Asset, entry.Direction, entry.Amount)).ToArray();
                    var persistedTrade = new Trade(TradeId.Create(existingTrade.TradeId), OrderId.Create(existingTrade.TakerOrderId), OrderId.Create(existingTrade.MakerOrderId), UserId.Create(existingTrade.BuyerUserId), UserId.Create(existingTrade.SellerUserId), existingTrade.Quantity, BrlCents.Create(existingTrade.Price), AcceptedSequence.Create(existingTrade.AcceptedSequence), existingTrade.FillOrdinal);
                    var disposition = SettlementValidator.CompareTrade(expectedTrade, persistedTrade, existingLedger);
                    if (disposition == DuplicateTradeDisposition.Conflicting) throw new SubmissionConflictException("Trade identity conflicts with persisted content.");
                    if (disposition == DuplicateTradeDisposition.IdenticalComplete)
                    {
                        responses.Add(new(fill.TradeId.Value, fill.Quantity, fill.Price.Value));
                        continue;
                    }
                    throw new InvalidOperationException("Persisted trade has incomplete ledger entries.");
                }
                var cost = checked(fill.Quantity * fill.Price.Value);
                var walletUpdates = await connection.ExecuteAsync(new CommandDefinition("UPDATE wallets SET brl_locked=brl_locked-@cost,vibranium_available=vibranium_available+@quantity,version=version+1,updated_at=now() WHERE user_id=@buyer AND brl_locked>=@cost; UPDATE wallets SET vibranium_locked=vibranium_locked-@quantity,brl_available=brl_available+@cost,version=version+1,updated_at=now() WHERE user_id=@seller AND vibranium_locked>=@quantity", new { buyer, seller, cost, quantity = fill.Quantity }, transaction, cancellationToken: cancellationToken));
                if (walletUpdates != 2) throw new SubmissionRejectedException("Reservation is insufficient for settlement.");
                var ledgerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
                var changed = await connection.ExecuteAsync(new CommandDefinition("INSERT INTO trades(id,taker_order_id,maker_order_id,buyer_user_id,seller_user_id,quantity,price_brl_cents,accepted_sequence,fill_ordinal) VALUES(@id,@taker,@maker,@buyer,@seller,@quantity,@price,@sequence,@ordinal); INSERT INTO ledger_entries(id,trade_id,effect_type,user_id,asset,direction,amount) VALUES(@l1,@id,'BUYER_BRL_DEBIT',@buyer,'BRL','DEBIT',@cost),(@l2,@id,'BUYER_VIBRANIUM_CREDIT',@buyer,'VIBRANIUM','CREDIT',@quantity),(@l3,@id,'SELLER_VIBRANIUM_DEBIT',@seller,'VIBRANIUM','DEBIT',@quantity),(@l4,@id,'SELLER_BRL_CREDIT',@seller,'BRL','CREDIT',@cost); UPDATE orders SET remaining_quantity=@makerRemaining,status=@makerStatus,updated_at=now() WHERE id=@maker; UPDATE reservations SET remaining_amount=remaining_amount-@makerReservation,status=CASE WHEN remaining_amount-@makerReservation=0 THEN 'RELEASED' ELSE 'ACTIVE' END,updated_at=now() WHERE order_id=@makerOrder AND remaining_amount>=@makerReservation; UPDATE reservations SET remaining_amount=remaining_amount-@takerReservation,status=CASE WHEN remaining_amount-@takerReservation=0 THEN 'RELEASED' ELSE 'ACTIVE' END,updated_at=now() WHERE order_id=@takerOrder AND remaining_amount>=@takerReservation", new { id = fill.TradeId.Value, taker = fill.TakerOrderId.Value, maker = fill.MakerOrderId.Value, buyer, seller, quantity = fill.Quantity, price = fill.Price.Value, sequence, ordinal = fill.FillOrdinal, cost, l1 = ledgerIds[0], l2 = ledgerIds[1], l3 = ledgerIds[2], l4 = ledgerIds[3], makerRemaining = maker.RemainingQuantity, makerStatus = maker.Status.ToString(), makerOrder = maker.Id.Value, takerOrder = order.Id.Value, makerReservation = maker.Side == Side.BUY ? checked(fill.Quantity * maker.LimitPrice.Value) : fill.Quantity, takerReservation = order.Side == Side.BUY ? checked(fill.Quantity * order.LimitPrice.Value) : fill.Quantity }, transaction, cancellationToken: cancellationToken));
                if (changed < 5) throw new InvalidOperationException("Settlement did not update all expected rows.");
                var buyerLimit = fill.TakerSide == Side.BUY ? command.Price.Value : maker.LimitPrice.Value;
                if (fill.Price.Value < buyerLimit)
                {
                    var release = checked(fill.Quantity * (buyerLimit - fill.Price.Value));
                    var buyerUser = fill.TakerSide == Side.BUY ? command.UserId.Value : maker.UserId.Value;
                    if (await connection.ExecuteAsync(new CommandDefinition("UPDATE wallets SET brl_locked=brl_locked-@release,brl_available=brl_available+@release WHERE user_id=@user AND brl_locked>=@release", new { release, user = buyerUser }, transaction, cancellationToken: cancellationToken)) != 1) throw new SubmissionRejectedException("Reserved BRL release is insufficient.");
                }
                if (maker.RemainingQuantity == 0)
                    await ReleaseResidualAsync(connection, transaction, maker.Id, maker.UserId, maker.Side, cancellationToken);
                responses.Add(new(fill.TradeId.Value, fill.Quantity, fill.Price.Value));
            }
            await connection.ExecuteAsync(new CommandDefinition("UPDATE orders SET remaining_quantity=@remaining,status=@status,updated_at=now() WHERE id=@id", new { id = order.Id.Value, remaining = order.RemainingQuantity, status = order.Status.ToString() }, transaction, cancellationToken: cancellationToken));
            if (order.RemainingQuantity == 0)
            {
                await ReleaseResidualAsync(connection, transaction, order.Id, order.UserId, order.Side, cancellationToken);
            }
            var output = new SubmitOrderResult(order.Id.Value, command.UserId.Value, command.Side.ToString(), order.Status.ToString(), order.OriginalQuantity.Value, order.ExecutedQuantity, order.RemainingQuantity, sequence, responses, 201, false);
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO idempotency_records(user_id,idempotency_key,canonical_payload_hash,order_id,response_status,response_body) VALUES(@user,@key,@hash,@order,@status,CAST(@body AS jsonb))", new { user = command.UserId.Value, key = command.IdempotencyKey.Value, hash = command.CanonicalHash, order = order.Id.Value, status = 201, body = JsonSerializer.Serialize(output) }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            RecordTransactionMetrics(transactionStarted);
            book.ReplaceWith(candidate);
            return output;
        }
        catch (Exception exception)
        {
            databaseActivity?.SetStatus(ActivityStatusCode.Error, "database transaction failed");
            databaseActivity?.AddException(exception);
            try { await transaction.RollbackAsync(cancellationToken); }
            finally { RecordTransactionMetrics(transactionStarted); }
            throw;
        }
    }

    private void RecordTransactionMetrics(long started)
    {
        metrics.DatabaseBatchFlushDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds);
        metrics.DatabaseBatchSize.Record(1);
    }

    private static async Task ReleaseResidualAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, OrderId orderId, UserId userId, Side side, CancellationToken cancellationToken)
    {
        // RETURNING exposes the value after UPDATE. Read the old value first so a
        // residual is not silently left locked (or released twice).
        var residual = await connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT remaining_amount FROM reservations WHERE order_id=@order FOR UPDATE",
            new { order = orderId.Value }, transaction, cancellationToken: cancellationToken));
        if (residual is null || residual.Value == 0)
            return;

        if (await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE reservations SET remaining_amount=0,status='RELEASED',updated_at=now() WHERE order_id=@order AND remaining_amount=@amount",
                new { order = orderId.Value, amount = residual.Value }, transaction, cancellationToken: cancellationToken)) != 1)
            throw new InvalidOperationException("Reservation residual could not be released atomically.");

        var releaseSql = side == Side.BUY
            ? "UPDATE wallets SET brl_locked=brl_locked-@amount,brl_available=brl_available+@amount WHERE user_id=@user AND brl_locked>=@amount"
            : "UPDATE wallets SET vibranium_locked=vibranium_locked-@amount,vibranium_available=vibranium_available+@amount WHERE user_id=@user AND vibranium_locked>=@amount";
        if (await connection.ExecuteAsync(new CommandDefinition(releaseSql, new { amount = residual.Value, user = userId.Value }, transaction, cancellationToken: cancellationToken)) != 1)
            throw new SubmissionRejectedException("Reservation release is insufficient.");
    }

    private async Task<SubmitOrderResult> PersistRejectionAsync(SubmitOrderCommand command, string reason, CancellationToken cancellationToken)
    {
        await using var connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var transactionStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<StoredIdempotency>(new CommandDefinition("SELECT response_status Status,response_body::text Body,canonical_payload_hash Hash FROM idempotency_records WHERE user_id=@user AND idempotency_key=@key FOR UPDATE", new { user = command.UserId.Value, key = command.IdempotencyKey.Value }, transaction, cancellationToken: cancellationToken));
            if (existing is not null)
            {
                if (!existing.Hash.AsSpan().SequenceEqual(command.CanonicalHash)) throw new SubmissionConflictException("Idempotency key was used with a different payload.");
                var replay = JsonSerializer.Deserialize<SubmitOrderResult>(existing.Body) ?? throw new InvalidOperationException("Persisted idempotency response is invalid.");
                await transaction.CommitAsync(cancellationToken);
                RecordTransactionMetrics(transactionStarted);
                return replay with { Replay = true, HttpStatus = 200 };
            }
            var orderId = RejectionOrderId(command);
            var output = new SubmitOrderResult(orderId, command.UserId.Value, command.Side.ToString(), "REJECTED", command.Quantity.Value, 0, command.Quantity.Value, null, [], 409, false);
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO orders(id,user_id,instrument,side,limit_price_brl_cents,original_quantity,remaining_quantity,status,accepted_sequence) VALUES(@id,@user,'VIBRANIUM',@side,@price,@quantity,@quantity,'REJECTED',NULL); INSERT INTO idempotency_records(user_id,idempotency_key,canonical_payload_hash,order_id,response_status,response_body) VALUES(@user,@key,@hash,@id,409,CAST(@body AS jsonb))", new { id = orderId, user = command.UserId.Value, side = command.Side.ToString(), price = command.Price.Value, quantity = command.Quantity.Value, key = command.IdempotencyKey.Value, hash = command.CanonicalHash, body = JsonSerializer.Serialize(output) }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            RecordTransactionMetrics(transactionStarted);
            logger.LogInformation("Order rejected by domain rule: {Reason}", SensitiveDataRedactor.Redact(reason));
            return output;
        }
        catch
        {
            try { await transaction.RollbackAsync(cancellationToken); }
            finally { RecordTransactionMetrics(transactionStarted); }
            throw;
        }
    }

    private static Guid RejectionOrderId(SubmitOrderCommand command)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{command.UserId.Value:D}|{command.IdempotencyKey.Value}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
