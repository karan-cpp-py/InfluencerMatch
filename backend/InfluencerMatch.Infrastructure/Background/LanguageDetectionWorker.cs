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
    /// Periodically runs language detection for all creators.
    /// Initial delay: 5 minutes (let the app start fully).
    /// Interval: 6 hours (language rarely changes; 3 API units × N creators kept within quota).
    /// </summary>
    public class LanguageDetectionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LanguageDetectionWorker> _logger;

        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan Interval     = TimeSpan.FromHours(6);

        public LanguageDetectionWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<LanguageDetectionWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LanguageDetectionWorker: initial delay {D}", InitialDelay);
            await Task.Delay(InitialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope   = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ILanguageDetectionService>();
                    await service.RefreshAllAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LanguageDetectionWorker: unhandled error");
                }

                _logger.LogInformation("LanguageDetectionWorker: next run in {I}", Interval);
                await Task.Delay(Interval, stoppingToken);
            }
        }
    }
}
