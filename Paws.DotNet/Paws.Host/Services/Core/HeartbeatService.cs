using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Paws.Host.Services.Core
{
    public class HeartbeatService : BackgroundService
    {
        private readonly ILogger<HeartbeatService> _logger;

        public HeartbeatService(ILogger<HeartbeatService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Heartbeat Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("[Heartbeat]");
                await Task.Delay(1000, stoppingToken);
            }

            _logger.LogInformation("Heartbeat Service is stopping.");
        }
    }
}
