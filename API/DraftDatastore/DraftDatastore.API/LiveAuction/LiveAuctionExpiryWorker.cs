namespace DraftDatastore.API.LiveAuction;

/// <summary>Closes lots the moment their timer ends, even when nobody is polling.</summary>
public sealed class LiveAuctionExpiryWorker(IServiceScopeFactory scopes, ILogger<LiveAuctionExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<LiveAuctionSettlement>().SettleExpiredAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Expired live lots could not be settled this pass; retrying shortly.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
    }
}
