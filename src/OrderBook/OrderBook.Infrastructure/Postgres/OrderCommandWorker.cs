using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace OrderBook.Infrastructure.Postgres;

public sealed class OrderCommandWorker(PostgresOrderSubmission submission, ILogger<OrderCommandWorker> logger, System.Diagnostics.ActivitySource activitySource) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Normal shutdown completes the channel and lets admitted commands
            // drain. Ownership loss is the exceptional path that cancels the
            // queue through ProcessingCancellation.
            await foreach (var command in submission.Queue.ReadAllAsync(CancellationToken.None))
            {
                Activity? activity = null;
                try
                {
                    activity = activitySource.StartActivity("orderbook.command");
                    using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(submission.Queue.ProcessingCancellation);
                    await submission.ProcessAsync(command, commandCancellation.Token);
                }
                catch (Exception exception)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "command failed");
                    activity?.AddException(exception);
                    logger.LogError(exception, "Unhandled order command failure.");
                    command.Completion.TrySetException(exception);
                }
                finally { activity?.Dispose(); }
            }
        }
        catch (OperationCanceledException) when (submission.Queue.ProcessingCancellation.IsCancellationRequested) { }
    }
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        submission.Queue.Complete();
        await base.StopAsync(cancellationToken);
    }
}
