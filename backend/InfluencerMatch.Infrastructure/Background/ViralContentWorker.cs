using System;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Background
{
    /// <summary>
    /// Runs ViralContentService.RefreshViralScoresAsync on a periodic schedule.
    /// Initial delay: 3 minutes (let the app fully start).
    /// Interval: every 2 hours.
    /// </summary>
    public class ViralContentWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ViralContentWorker> _logger;

        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(3);
        private static readonly TimeSpan Interval = TimeSpan.FromHours(2);

        public ViralContentWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<ViralContentWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ViralContentWorker: waiting {Delay} before first run", InitialDelay);
            await Task.Delay(InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IViralContentService>();
                    await service.RefreshViralScoresAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ViralContentWorker: cancellation requested, exiting");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ViralContentWorker: unhandled error, will retry after {Interval}", Interval);
                }

                _logger.LogInformation("ViralContentWorker: next run in {Interval}", Interval);
                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
