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
        using (var scope = _services.CreateScope())
        {
            var slotGenService = scope.ServiceProvider.GetRequiredService<GenerateSlotService>();
            slotGenService.InitializeSlots();
        }

        // Calculate initial delay until next midnight
        var tmpNow = DateTime.Now;
        var nextMidnight = DateTime.Today.AddDays(1);
        var initialDelay = nextMidnight - tmpNow;

        await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _services.CreateScope())
            {
                var slotGenService = scope.ServiceProvider.GetRequiredService<GenerateSlotService>();
                slotGenService.StartSlotGeneration(DateTime.Now);
            }
            
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken); // Wait 24 hours until next run
        }
    }
}