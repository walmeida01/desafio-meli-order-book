using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderBook.Application.Ports;
using OrderBook.Domain.Modules.Matching;
using OrderBook.Infrastructure.Observability;

namespace OrderBook.Infrastructure.Postgres;

public sealed class WriterRuntime(MigrationRunner migrations, PostgresWriterOwnership ownership, OrderBookRecovery recovery, global::OrderBook.Domain.Modules.Matching.OrderBook book, RuntimeReadiness readiness, AdmissionState admission, PostgresOrderSubmission submission, ILogger<WriterRuntime> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await migrations.ApplyAsync(stoppingToken);
            if (!await ownership.AcquireAsync(stoppingToken)) return;
            await recovery.RebuildAsync(book, stoppingToken);
            readiness.MarkReady();
            admission.Start();
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                if (!await ownership.CheckAsync(stoppingToken))
                {
                    readiness.MarkNotReady();
                    admission.Stop();
                    submission.StopProcessing();
                    logger.LogCritical("PostgreSQL writer ownership was lost; mutations are stopped.");
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) { readiness.MarkNotReady(); admission.Stop(); logger.LogError("Writer recovery failed; readiness remains false: {Error}", SensitiveDataRedactor.Redact(exception.Message)); }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        admission.Stop();
        readiness.MarkNotReady();
        submission.Queue.Complete();
        await base.StopAsync(cancellationToken);
        await ownership.ReleaseAsync(cancellationToken);
    }
}
