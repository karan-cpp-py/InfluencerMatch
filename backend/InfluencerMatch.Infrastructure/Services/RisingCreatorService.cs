using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.Infrastructure.Services
{
    /// <summary>
    /// Feature 1 — Rising Creator Detection.
    ///
    /// GrowthRate = (SubscribersNow − Subscribers30DaysAgo) / Subscribers30DaysAgo
    ///
    /// Categorisation thresholds:
    ///   Rising    → rate ≥ 0.05  (5 %+ monthly)
    ///   Stable    → -0.02 < rate < 0.05
    ///   Declining → rate ≤ -0.02
    /// </summary>
    public class RisingCreatorService : IRisingCreatorService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RisingCreatorService> _logger;

        private const double RisingThreshold = 0.05;
        private const double DecliningThreshold = -0.02;

        public RisingCreatorService(ApplicationDbContext db, ILogger<RisingCreatorService> logger)
        {
            _db = db;
            _logger = logger;
        }

        // ── RecalculateAllGrowthScoresAsync ───────────────────────────────────

        public async Task RecalculateAllGrowthScoresAsync(CancellationToken ct = default)
        {
            var creators = await _db.Creators
                .AsNoTracking()
                .Where(c => c.ChannelId != null && c.ChannelId != "")
                .ToListAsync(ct);

            _logger.LogInformation("RisingCreatorService: recalculating growth scores for {N} creators", creators.Count);

            // 1. Load all CreatorGrowth snapshots in one DB round-trip
            var allSnapshots = (await _db.CreatorGrowth
                .AsNoTracking()
                .OrderBy(g => g.RecordedAt)
                .ToListAsync(ct))
                .GroupBy(g => g.CreatorId)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.RecordedAt).ToList());

            // 2. Compute monthly growth rate per creator.
            // If history is weak/flat, fall back to a momentum proxy from engagement + avg views.
            var rawScores = creators.Select(creator =>
            {
                allSnapshots.TryGetValue(creator.CreatorId, out var snaps);

                double rate  = 0;
                long   delta = 0;
                long   baseSubs = creator.Subscribers;

                if (snaps != null && snaps.Count >= 2)
                {
                    var latest = snaps.Last();
                    var baselineTargetDate = latest.RecordedAt.AddDays(-30);
                    var baseline = snaps
                        .OrderBy(s => Math.Abs((s.RecordedAt - baselineTargetDate).TotalDays))
                        .First();
                    if (baseline.GrowthId == latest.GrowthId)
                    {
                        baseline = snaps.First();
                    }

                    baseSubs = baseline.Subscribers;
                    delta = latest.Subscribers - baseSubs;
                    double days = Math.Max((latest.RecordedAt - baseline.RecordedAt).TotalDays, 1);
                    rate  = baseSubs > 0 ? delta / (double)baseSubs * (30.0 / days) : 0;
                }

                if (Math.Abs(rate) < 0.001)
                {
                    var proxy = ComputeMomentumProxyRate(creator);
                    rate = proxy;

                    if (delta == 0 && baseSubs > 0)
                    {
                        delta = (long)Math.Round(baseSubs * proxy);
                    }
                }

                return (creator.CreatorId, rate, delta, baseSubs);
            }).ToList();

            // 4. Upsert to DB
            foreach (var (creatorId, rate, delta, baseSubs) in rawScores)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await UpsertGrowthScoreAsync(creatorId, rate, Categorize(rate), delta, baseSubs, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Growth score upsert failed for creator {Id}", creatorId);
                }
            }

            _logger.LogInformation("RisingCreatorService: recalculation complete");
        }

        private async Task UpsertGrowthScoreAsync(
            int creatorId, double rate, string category,
            long subscriberDelta, long baselineSubscribers, CancellationToken ct)
        {
            var existing = await _db.CreatorGrowthScores
                .FirstOrDefaultAsync(s => s.CreatorId == creatorId, ct);

            if (existing == null)
            {
                _db.CreatorGrowthScores.Add(new CreatorGrowthScore
                {
                    CreatorId           = creatorId,
                    GrowthRate          = rate,
                    GrowthCategory      = category,
                    SubscriberDelta     = subscriberDelta,
                    BaselineSubscribers = baselineSubscribers,
                    CalculatedAt        = DateTime.UtcNow
                });
            }
            else
            {
                existing.GrowthRate          = rate;
                existing.GrowthCategory      = category;
                existing.SubscriberDelta     = subscriberDelta;
                existing.BaselineSubscribers = baselineSubscribers;
                existing.CalculatedAt        = DateTime.UtcNow;
                _db.CreatorGrowthScores.Update(existing);
            }
            await _db.SaveChangesAsync(ct);
        }

        // ── GetRisingCreatorsAsync ────────────────────────────────────────────

        public async Task<List<RisingCreatorDto>> GetRisingCreatorsAsync(
            int topN = 50,
            string? growthCategory = null,
            string? country = null)
        {
            // normalise empty string to null so filters work correctly
            if (string.IsNullOrWhiteSpace(growthCategory)) growthCategory = null;
            if (string.IsNullOrWhiteSpace(country))        country        = null;

            // Join CreatorGrowthScores with Creators + optional CreatorAnalytics
            var query =
                from gs in _db.CreatorGrowthScores.AsNoTracking()
                join c  in _db.Creators.AsNoTracking().Where(c => c.ChannelId != null && c.ChannelId != "") on gs.CreatorId equals c.CreatorId
                join a  in _db.CreatorAnalytics.AsNoTracking() on c.CreatorId equals a.CreatorId into aj
                from a  in aj.DefaultIfEmpty()
                select new { gs, c, a };

            if (!string.IsNullOrWhiteSpace(growthCategory))
                query = query.Where(x => x.gs.GrowthCategory == growthCategory);

            if (!string.IsNullOrWhiteSpace(country))
                query = query.Where(x => x.c.Country == country);

            var rows = await query
                .OrderByDescending(x => x.gs.GrowthRate)
                .Take(topN)
                .ToListAsync();

            // If the table is empty (no background run yet), compute on-the-fly
            if (rows.Count == 0)
                return await ComputeRisingOnTheFlyAsync(topN, growthCategory, country);

            return rows.Select(x => new RisingCreatorDto
            {
                CreatorId       = x.c.CreatorId,
                ChannelName     = x.c.ChannelName  ?? string.Empty,
                Platform        = x.c.Platform,
                Category        = x.c.Category     ?? string.Empty,
                Country         = x.c.Country      ?? string.Empty,
                Subscribers     = x.c.Subscribers,
                GrowthRate      = x.gs.GrowthRate,
                GrowthCategory  = x.gs.GrowthCategory,
                SubscriberDelta = x.gs.SubscriberDelta,
                EngagementRate  = EngagementRateEstimator.EstimateOrStored(
                    x.a?.EngagementRate,
                    x.c.Subscribers,
                    x.c.TotalViews,
                    x.c.VideoCount,
                    x.a?.AvgViews),
                CalculatedAt    = x.gs.CalculatedAt
            }).ToList();
        }

        /// <summary>
        /// Fallback: compute growth on-the-fly from CreatorGrowth snapshots
        /// without writing to the DB (called when CreatorGrowthScores is empty).
        /// </summary>
        private async Task<List<RisingCreatorDto>> ComputeRisingOnTheFlyAsync(
            int topN, string? growthCategory, string? country)
        {
            var creators = await _db.Creators.AsNoTracking()
                .Where(c => c.ChannelId != null && c.ChannelId != "" && (country == null || c.Country == country))
                .ToListAsync();

            if (!creators.Any()) return new List<RisingCreatorDto>();

            var ids = creators.Select(c => c.CreatorId).ToList();

            var analyticsMap = await _db.CreatorAnalytics.AsNoTracking()
                .Where(a => ids.Contains(a.CreatorId))
                .ToDictionaryAsync(a => a.CreatorId);

            var allSnaps = (await _db.CreatorGrowth.AsNoTracking()
                .Where(g => ids.Contains(g.CreatorId))
                .OrderBy(g => g.RecordedAt)
                .ToListAsync())
                .GroupBy(g => g.CreatorId)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.RecordedAt).ToList());

            // Compute monthly growth rate per creator with momentum fallback.
            var raw = creators.Select(creator =>
            {
                analyticsMap.TryGetValue(creator.CreatorId, out var a);
                allSnaps.TryGetValue(creator.CreatorId, out var snaps);

                double rate  = 0;
                long   delta = 0;
                long   base_ = creator.Subscribers;

                if (snaps != null && snaps.Count >= 2)
                {
                    var oldest = snaps.First();
                    var newest = snaps.Last();
                    double days = Math.Max((newest.RecordedAt - oldest.RecordedAt).TotalDays, 1);
                    base_ = oldest.Subscribers;
                    delta = newest.Subscribers - oldest.Subscribers;
                    rate  = base_ > 0 ? delta / (double)base_ * (30.0 / days) : 0;
                }

                if (Math.Abs(rate) < 0.001)
                {
                    rate = ComputeMomentumProxyRate(creator);
                    if (delta == 0 && base_ > 0)
                    {
                        delta = (long)Math.Round(base_ * rate);
                    }
                }

                return (creator, a, rate, delta, base_);
            }).ToList();

            var results = raw
                .Select(r => new RisingCreatorDto
                {
                    CreatorId       = r.creator.CreatorId,
                    ChannelName     = r.creator.ChannelName ?? string.Empty,
                    Platform        = r.creator.Platform,
                    Category        = r.creator.Category   ?? string.Empty,
                    Country         = r.creator.Country    ?? string.Empty,
                    Subscribers     = r.creator.Subscribers,
                    GrowthRate      = r.rate,
                    GrowthCategory  = Categorize(r.rate),
                    SubscriberDelta = r.delta,
                    EngagementRate  = EngagementRateEstimator.EstimateOrStored(
                        r.a?.EngagementRate,
                        r.creator.Subscribers,
                        r.creator.TotalViews,
                        r.creator.VideoCount,
                        r.a?.AvgViews),
                    CalculatedAt    = DateTime.UtcNow
                })
                .Where(r => growthCategory == null || r.GrowthCategory == growthCategory)
                .OrderByDescending(r => r.GrowthRate)
                .Take(topN)
                .ToList();

            return results;
        }

        private static string Categorize(double rate)
        {
            if (rate >= RisingThreshold) return "Rising";
            if (rate <= DecliningThreshold) return "Declining";
            return "Stable";
        }

        private static double ComputeMomentumProxyRate(Creator creator)
        {
            var engagement = Math.Clamp(creator.EngagementRate, 0, 0.2); // ratio form
            var avgViews = Math.Max(creator.AvgViews, 0);
            var viewsPerSub = creator.Subscribers > 0 ? Math.Clamp(avgViews / creator.Subscribers, 0, 2) : 0;
            var freshnessBoost = creator.LastRefreshedAt.HasValue
                ? Math.Clamp(1.0 - (DateTime.UtcNow - creator.LastRefreshedAt.Value).TotalDays / 60.0, 0, 1)
                : 0.35;

            var proxy = (engagement * 0.8) + (viewsPerSub * 0.12) + (freshnessBoost * 0.03);
            return Math.Clamp(proxy, 0.005, 0.18);
        }
    }
}
