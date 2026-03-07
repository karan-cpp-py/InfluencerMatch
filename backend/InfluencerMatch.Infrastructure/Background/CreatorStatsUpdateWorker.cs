using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Background
{
    /// <summary>
    /// Runs every 24 hours and refreshes subscriber/view/video-count stats
    /// for all registered creator channels, using batched Channels API calls
    /// (up to 50 channel IDs per request = 1 quota unit per batch).
    /// </summary>
    public class CreatorStatsUpdateWorker : BackgroundService
    {
        private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(7);
        private static readonly TimeSpan Interval     = TimeSpan.FromHours(24);

        private readonly IServiceProvider _services;
        private readonly IYouTubeQuotaTracker _quota;
        private readonly ILogger<CreatorStatsUpdateWorker> _logger;

        public CreatorStatsUpdateWorker(
            IServiceProvider services,
            IYouTubeQuotaTracker quota,
            ILogger<CreatorStatsUpdateWorker> logger)
        {
            _services = services;
            _quota    = quota;
            _logger   = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(InitialDelay, ct);

            while (!ct.IsCancellationRequested)
            {
                try { await RunCycleAsync(ct); }
                catch (Exception ex) { _logger.LogError(ex, "CreatorStatsUpdateWorker cycle error"); }
                await Task.Delay(Interval, ct);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db      = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var http    = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var config  = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var apiKey  = config["YouTube:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey)
                || apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase)) return;

            var channels = await db.CreatorChannels.ToListAsync(ct);
            if (!channels.Any()) return;

            var client = http.CreateClient();
            var updated = 0;

            foreach (var batch in channels.Chunk(50))
            {
                if (!_quota.CanConsume(1))
                {
                    _logger.LogWarning("Quota limit reached ({U}/{L}), halting CreatorStatsUpdate",
                        _quota.UsedToday, _quota.DailyLimit);
                    break;
                }

                var ids = string.Join(",", batch.Select(c => c.ChannelId));
                try
                {
                    var url  = $"https://www.googleapis.com/youtube/v3/channels"
                             + $"?part=statistics&id={ids}&key={apiKey}";
                    var resp = await client.GetFromJsonAsync<YtChannelResponse>(url, ct);
                    _quota.Consume(1);

                    if (resp?.items == null) continue;

                    foreach (var item in resp.items)
                    {
                        var ch = batch.FirstOrDefault(c => c.ChannelId == item.id);
                        if (ch == null) continue;

                        var subs = item.statistics?.subscriberCount ?? ch.Subscribers;
                        ch.Subscribers          = subs;
                        ch.TotalViews           = item.statistics?.viewCount  ?? ch.TotalViews;
                        ch.VideoCount           = item.statistics?.videoCount ?? ch.VideoCount;
                        ch.CreatorTier          = Creator.ComputeTier(subs);
                        ch.LastStatsUpdatedAt   = DateTime.UtcNow;
                        db.CreatorChannels.Update(ch);
                        updated++;
                    }
                    await db.SaveChangesAsync(ct);
                    await Task.Delay(200, ct); // polite pacing
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CreatorStatsUpdate: batch error");
                }
            }
            _logger.LogInformation("CreatorStatsUpdateWorker refreshed {N} channels", updated);
        }

        // ── private response DTO ─────────────────────────────────────────────
        private record YtChannelResponse(System.Collections.Generic.List<YtChannelItem>? items);
        private record YtChannelItem(string id, YtStats? statistics);
        private record YtStats(long? subscriberCount, long? viewCount, int? videoCount);
    }
}
