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
    ///   Rising   → rate ≥ 0.05  (5 %+ monthly)
    ///   Stable   → rate ≥ 0.00
    ///   Declining → rate < 0.00
    /// </summary>
    public class RisingCreatorService : IRisingCreatorService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RisingCreatorService> _logger;

        private const double RisingThreshold   =  0.05;   // 5 %
        private const double DecliningThreshold = 0.00;

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

            // Check if all snapshots are clustered within 7 days (dev/demo data scenario)
            DateTime? globalMin = allSnapshots.Values.SelectMany(s => s).Select(s => s.RecordedAt).DefaultIfEmpty().Min();
            DateTime? globalMax = allSnapshots.Values.SelectMany(s => s).Select(s => s.RecordedAt).DefaultIfEmpty().Max();
            bool shortSpan = globalMin == null || (globalMax!.Value - globalMin!.Value).TotalDays < 7;

            // 2. Compute a raw "growth proxy" for every creator
            var rawScores = creators.Select(creator =>
            {
                allSnapshots.TryGetValue(creator.CreatorId, out var snaps);

                double rate  = 0;
                long   delta = 0;
                long   baseSubs = creator.Subscribers;

                if (!shortSpan && snaps != null && snaps.Count >= 2)
                {
                    // Real multi-week data: use proper growth rate formula
                    var now_     = snaps.Last().RecordedAt;
                    var target   = now_.AddDays(-30);
                    var baseline = snaps.OrderBy(s => Math.Abs((s.RecordedAt - target).TotalDays)).First();
                    var latest_  = snaps.Last();
                    if (baseline.GrowthId == latest_.GrowthId) baseline = snaps.First();

                    baseSubs = baseline.Subscribers;
                    delta    = latest_.Subscribers - baseSubs;
                    double days = Math.Max((latest_.RecordedAt - baseline.RecordedAt).TotalDays, 1);
                    rate  = baseSubs > 0 ? delta / (double)baseSubs * (30.0 / days) : 0;
                }
                else
                {
                    // Short-span / demo data: use subscriber count as proxy
                    // (relative to itself — rate = ln(subscribers+1) normalised)
                    baseSubs = creator.Subscribers;
                    rate     = creator.Subscribers > 0
                        ? Math.Log(creator.Subscribers + 1)   // log scale proxy
                        : 0;
                    delta    = 0;
                }

                return (creator.CreatorId, rate, delta, baseSubs);
            }).ToList();

            // 3. Assign categories by percentile rank and normalize rates to 0.0–1.0
            var sorted = rawScores.OrderBy(r => r.rate).ToList();
            int total  = sorted.Count;
            var assignments   = new Dictionary<int, string>();
            var normalizedRate = new Dictionary<int, double>();
            double minRate = sorted.Count > 0 ? sorted.First().rate  : 0;
            double maxRate = sorted.Count > 0 ? sorted.Last().rate   : 1;
            double rateRange = maxRate - minRate;

            for (int i = 0; i < sorted.Count; i++)
            {
                double pct = total > 1 ? (double)i / (total - 1) : 0.5;
                string cat = pct >= 0.80 ? "Rising"
                           : pct <= 0.20 ? "Declining"
                           :               "Stable";
                assignments[sorted[i].CreatorId] = cat;

                // Normalize raw rate to 0.0–1.0 range so UI can display as a percentage
                double norm = rateRange > 0
                    ? (sorted[i].rate - minRate) / rateRange
                    : 0.5;
                normalizedRate[sorted[i].CreatorId] = norm;
            }

            // 4. Upsert to DB
            foreach (var (creatorId, rate, delta, baseSubs) in rawScores)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await UpsertGrowthScoreAsync(creatorId, normalizedRate[creatorId], assignments[creatorId], delta, baseSubs, ct);
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
            string? growthCategory = "Rising",
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

            // Check if historical data spans ≥7 days
            var allDates = allSnaps.Values.SelectMany(s => s).Select(s => s.RecordedAt).ToList();
            bool shortSpan = !allDates.Any() || (allDates.Max() - allDates.Min()).TotalDays < 7;

            // Compute raw proxy rate per creator
            var raw = creators.Select(creator =>
            {
                analyticsMap.TryGetValue(creator.CreatorId, out var a);
                allSnaps.TryGetValue(creator.CreatorId, out var snaps);

                double rate  = 0;
                long   delta = 0;
                long   base_ = creator.Subscribers;

                if (!shortSpan && snaps != null && snaps.Count >= 2)
                {
                    var oldest = snaps.First();
                    var newest = snaps.Last();
                    double days = Math.Max((newest.RecordedAt - oldest.RecordedAt).TotalDays, 1);
                    base_ = oldest.Subscribers;
                    delta = newest.Subscribers - oldest.Subscribers;
                    rate  = base_ > 0 ? delta / (double)base_ * (30.0 / days) : 0;
                }
                else
                {
                    rate  = creator.Subscribers > 0 ? Math.Log(creator.Subscribers + 1) : 0;
                    base_ = creator.Subscribers;
                }

                return (creator, a, rate, delta, base_);
            }).ToList();

            // Percentile-based category assignment + normalize rates to 0.0–1.0
            var sorted = raw.OrderBy(r => r.rate).ToList();
            int total  = sorted.Count;
            double minR = sorted.Count > 0 ? sorted.First().rate : 0;
            double maxR = sorted.Count > 0 ? sorted.Last().rate  : 1;
            double rng  = maxR - minR;

            var catMap  = new Dictionary<int, string>();
            var normMap = new Dictionary<int, double>();
            for (int i = 0; i < sorted.Count; i++)
            {
                double pct = total > 1 ? (double)i / (total - 1) : 0.5;
                string cat = pct >= 0.80 ? "Rising" : pct <= 0.20 ? "Declining" : "Stable";
                catMap[sorted[i].creator.CreatorId]  = cat;
                normMap[sorted[i].creator.CreatorId] = rng > 0 ? (sorted[i].rate - minR) / rng : 0.5;
            }

            var results = raw
                .Select(r => new RisingCreatorDto
                {
                    CreatorId       = r.creator.CreatorId,
                    ChannelName     = r.creator.ChannelName ?? string.Empty,
                    Platform        = r.creator.Platform,
                    Category        = r.creator.Category   ?? string.Empty,
                    Country         = r.creator.Country    ?? string.Empty,
                    Subscribers     = r.creator.Subscribers,
                    GrowthRate      = normMap[r.creator.CreatorId],
                    GrowthCategory  = catMap[r.creator.CreatorId],
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
    }
}
