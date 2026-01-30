using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Paws.Host;

public class HeartbeatService : BackgroundService
{
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(ILogger<HeartbeatService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeartbeatService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Log the heartbeat. The frontend backend.ts watches for this specific string.
            // Using LogInformation ensures it appears in the console output redirected to Electron.
            _logger.LogInformation("[Heartbeat]");

            try
            {
                await Task.Delay(1000, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Graceful shutdown
                break;
            }
        }

        _logger.LogInformation("HeartbeatService stopped.");
    }
}
