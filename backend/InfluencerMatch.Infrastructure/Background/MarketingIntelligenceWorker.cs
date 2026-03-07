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
    /// Runs two jobs in sequence on startup (after a short delay) and then every 24 hours:
    ///   1. RecalculateAllScores  – computes / updates CreatorScore for every creator
    ///   2. ScanAllCreators       – scans YouTube videos for brand-promotion signals
    ///
    /// Both jobs use scoped services (IServiceScopeFactory) to avoid DbContext lifetime issues.
    /// </summary>
    public class MarketingIntelligenceWorker : BackgroundService
    {
        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan Interval     = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MarketingIntelligenceWorker> _logger;

        public MarketingIntelligenceWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<MarketingIntelligenceWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "MarketingIntelligenceWorker: first run in {Delay}", InitialDelay);

            try { await Task.Delay(InitialDelay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCycleAsync(stoppingToken);

                try { await Task.Delay(Interval, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }

            _logger.LogInformation("MarketingIntelligenceWorker: stopping.");
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            _logger.LogInformation("MarketingIntelligenceWorker: cycle started at {T}", DateTime.UtcNow);

            // ── Job 1: Creator scores ────────────────────────────────────────
            try
            {
                await using var scope1 = _scopeFactory.CreateAsyncScope();
                var scoring = scope1.ServiceProvider.GetRequiredService<ICreatorScoringService>();
                await scoring.RecalculateAllScoresAsync(ct);
                _logger.LogInformation("MarketingIntelligenceWorker: creator scores updated.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarketingIntelligenceWorker: scoring job failed.");
            }

            if (ct.IsCancellationRequested) return;

            // ── Job 2: Brand-promotion detection ────────────────────────────
            try
            {
                await using var scope2 = _scopeFactory.CreateAsyncScope();
                var brandSvc = scope2.ServiceProvider.GetRequiredService<IBrandPromotionService>();
                await brandSvc.ScanAllCreatorsAsync(ct);
                _logger.LogInformation("MarketingIntelligenceWorker: brand scan complete.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MarketingIntelligenceWorker: brand scan job failed.");
            }

            _logger.LogInformation("MarketingIntelligenceWorker: cycle finished at {T}", DateTime.UtcNow);
        }
    }
}
