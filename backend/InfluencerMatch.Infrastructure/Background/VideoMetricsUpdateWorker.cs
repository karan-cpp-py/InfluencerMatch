using System;
using System.Collections.Generic;
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
    /// Runs every 6 hours.
    /// For each registered creator channel, fetches fresh video-level stats
    /// (views, likes, comments) using the batched Videos API (50 IDs per request = 1 unit).
    /// A new <see cref="VideoMetrics"/> snapshot is inserted for every video —
    /// historical rows are never deleted for AI training purposes.
    /// Also recomputes the channel's EngagementRate from recent video performance.
    /// </summary>
    public class VideoMetricsUpdateWorker : BackgroundService
    {
        private static readonly TimeSpan InitialDelay   = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan Interval       = TimeSpan.FromHours(6);
        private const           int      VideosBatchSize = 50;

        private readonly IServiceProvider _services;
        private readonly IYouTubeQuotaTracker _quota;
        private readonly ILogger<VideoMetricsUpdateWorker> _logger;

        public VideoMetricsUpdateWorker(
            IServiceProvider services,
            IYouTubeQuotaTracker quota,
            ILogger<VideoMetricsUpdateWorker> logger)
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
                catch (Exception ex) { _logger.LogError(ex, "VideoMetricsUpdateWorker cycle error"); }
                await Task.Delay(Interval, ct);
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var http   = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            var apiKey = config["YouTube:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey)
                || apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase)) return;

            var now    = DateTime.UtcNow;
            var client = http.CreateClient();

            // Load all channel videos in one DB query
            var allVideos = await db.ChannelVideos.ToListAsync(ct);
            if (!allVideos.Any()) return;

            var metricsToAdd    = new List<VideoMetrics>();
            var updatedChannels = new HashSet<string>();
            var updatedCount    = 0;

            foreach (var batch in allVideos.Chunk(VideosBatchSize))
            {
                if (!_quota.CanConsume(1))
                {
                    _logger.LogWarning("Quota limit reached ({U}/{L}), halting VideoMetricsUpdate",
                        _quota.UsedToday, _quota.DailyLimit);
                    break;
                }

                var ids = string.Join(",", batch.Select(v => v.YoutubeVideoId));
                try
                {
                    var url  = $"https://www.googleapis.com/youtube/v3/videos"
                             + $"?part=statistics&id={ids}&key={apiKey}";
                    var resp = await client.GetFromJsonAsync<YtVideoListResponse>(url, ct);
                    _quota.Consume(1);

                    if (resp?.items == null) continue;

                    foreach (var item in resp.items)
                    {
                        var video = batch.FirstOrDefault(v => v.YoutubeVideoId == item.id);
                        if (video == null) continue;

                        video.ViewCount    = item.statistics?.viewCount    ?? video.ViewCount;
                        video.LikeCount    = item.statistics?.likeCount    ?? video.LikeCount;
                        video.CommentCount = item.statistics?.commentCount ?? video.CommentCount;
                        video.FetchedAt    = now;
                        db.ChannelVideos.Update(video);
                        updatedChannels.Add(video.ChannelId);
                        updatedCount++;

                        // Append a historical metrics snapshot (never deleted)
                        metricsToAdd.Add(new VideoMetrics
                        {
                            YoutubeVideoId = video.YoutubeVideoId,
                            Views          = video.ViewCount,
                            Likes          = video.LikeCount,
                            Comments       = video.CommentCount,
                            RecordedAt     = now
                        });
                    }
                    await Task.Delay(200, ct); // polite pacing
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VideoMetricsUpdate: batch error");
                }
            }

            if (metricsToAdd.Any())
                db.VideoMetrics.AddRange(metricsToAdd);

            await db.SaveChangesAsync(ct);

            // Recompute EngagementRate for each affected channel
            foreach (var channelId in updatedChannels)
            {
                await RecomputeEngagementRateAsync(db, channelId, ct);
            }
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "VideoMetricsUpdateWorker: {V} videos updated, {C} channels recomputed",
                updatedCount, updatedChannels.Count);
        }

        private static async Task RecomputeEngagementRateAsync(
            ApplicationDbContext db, string channelId, CancellationToken ct)
        {
            var ch = await db.CreatorChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId, ct);
            if (ch == null || ch.Subscribers == 0) return;

            // Average like rate over last 20 videos as engagement proxy
            var recent = await db.ChannelVideos
                .Where(v => v.ChannelId == channelId)
                .OrderByDescending(v => v.PublishedAt)
                .Take(20)
                .ToListAsync(ct);

            if (!recent.Any()) return;

            var avgLikes = recent.Average(v => (double)v.LikeCount);
            ch.EngagementRate = avgLikes / ch.Subscribers;
            db.CreatorChannels.Update(ch);
        }

        // ── private response DTOs ────────────────────────────────────────────
        private record YtVideoListResponse(List<YtVideoItem>? items);
        private record YtVideoItem(string? id, YtVideoStats? statistics);
        private record YtVideoStats(long? viewCount, long? likeCount, long? commentCount);
    }
}
