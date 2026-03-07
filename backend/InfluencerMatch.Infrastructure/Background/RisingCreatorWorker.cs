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
    /// Hosted background service that periodically re-calculates growth scores
    /// for all creators.
    ///
    /// Schedule:
    ///   • Initial delay : 5 minutes  (allow the API to warm up)
    ///   • Interval      : every 6 hours
    /// </summary>
    public class RisingCreatorWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RisingCreatorWorker> _logger;

        private static readonly TimeSpan InitialDelay    = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RecalcInterval  = TimeSpan.FromHours(6);

        public RisingCreatorWorker(IServiceScopeFactory scopeFactory, ILogger<RisingCreatorWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RisingCreatorWorker: starting — initial delay {Delay}", InitialDelay);

            try { await Task.Delay(InitialDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("RisingCreatorWorker: starting recalculation at {Now:u}", DateTime.UtcNow);

                    using var scope = _scopeFactory.CreateScope();
                    var service     = scope.ServiceProvider.GetRequiredService<IRisingCreatorService>();
                    await service.RecalculateAllGrowthScoresAsync(stoppingToken);

                    _logger.LogInformation("RisingCreatorWorker: recalculation complete. Next run in {Interval}", RecalcInterval);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RisingCreatorWorker: unhandled error during recalculation");
                }

                try { await Task.Delay(RecalcInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("RisingCreatorWorker: stopped");
        }
    }
}
