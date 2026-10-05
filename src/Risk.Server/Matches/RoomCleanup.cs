namespace Risk.Server.Matches;

public sealed class RoomCleanup(RoomRegistry registry) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken)) registry.Cleanup(DateTimeOffset.UtcNow);
    }
}
