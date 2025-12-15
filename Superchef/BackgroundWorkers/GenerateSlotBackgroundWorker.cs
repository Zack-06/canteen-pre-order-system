namespace Superchef.BackgroundWorkers;

public class GenerateSlotBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _services;

    public GenerateSlotBackgroundWorker(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Calculate initial delay until next midnight
        var now = DateTime.Now;
        var nextMidnight = DateTime.Today.AddDays(1);
        var initialDelay = nextMidnight - now;

        // await Task.Delay(initialDelay, stoppingToken);

        // while (!stoppingToken.IsCancellationRequested)
        // {
        //     using (var scope = _services.CreateScope())
        //     {
        //         var slotService = scope.ServiceProvider.GetRequiredService<>();
        //         await slotService.GenerateSlotsForAllStores(stoppingToken);
        //     }

        //     // Wait 24 hours until next run
        //     await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        // }
    }
}