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
    /// Daily background job that:
    ///   1. Records a subscriber-count growth snapshot for every creator.
    ///   2. Refreshes analytics (avg views, likes, comments, engagement rate).
    /// </summary>
    public class CreatorAnalyticsWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
        private readonly IServiceProvider _provider;
        private readonly ILogger<CreatorAnalyticsWorker> _logger;

        public CreatorAnalyticsWorker(IServiceProvider provider, ILogger<CreatorAnalyticsWorker> logger)
        {
            _provider = provider;
            _logger   = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay so the app fully starts before hitting external APIs
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
            catch (OperationCanceledException) { return; }

            await RunJobAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await RunJobAsync();
            }
        }

        private async Task RunJobAsync()
        {
            _logger.LogInformation("[CreatorAnalyticsWorker] Starting daily analytics + growth job at {Time}", DateTime.UtcNow);
            try
            {
                using var scope   = _provider.CreateScope();
                var analyticsService = scope.ServiceProvider.GetRequiredService<ICreatorAnalyticsService>();

                await analyticsService.RecordGrowthSnapshotAsync();
                await analyticsService.RefreshAllAnalyticsAsync();

                _logger.LogInformation("[CreatorAnalyticsWorker] Job completed at {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CreatorAnalyticsWorker] Job failed");
            }
        }
    }
}
