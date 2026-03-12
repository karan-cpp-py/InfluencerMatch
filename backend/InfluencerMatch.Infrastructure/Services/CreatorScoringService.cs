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
    /// Calculates composite creator scores.
    ///
    /// Score formula (each component normalised to 0–100 before weighting):
    ///   Score = 0.4 × Engagement  + 0.3 × Views  + 0.2 × Growth  + 0.1 × Frequency
    ///
    /// Normalisation ceilings (= 100 % of component):
    ///   Engagement : 10 % engagement rate
    ///   Views      : 5 000 000 average views
    ///   Growth     : 10 % monthly subscriber growth rate
    ///   Frequency  : 7 videos / week  (≈ 1 / day)
    /// </summary>
    public class CreatorScoringService : ICreatorScoringService
    {
        private const double MaxEngagement = 0.10;          // 10 %
        private const double MaxAvgViews   = 5_000_000.0;   // 5 M
        private const double MaxGrowth     = 0.10;          // 10 % monthly
        private const double MaxFrequency  = 7.0;           // videos / week

        private readonly ApplicationDbContext _db;
        private readonly ILogger<CreatorScoringService> _logger;

        public CreatorScoringService(
            ApplicationDbContext db,
            ILogger<CreatorScoringService> logger)
        {
            _db     = db;
            _logger = logger;
        }

        // ────────────────────────────────────────────────────────────────────
        // ICreatorScoringService
        // ────────────────────────────────────────────────────────────────────

        public async Task<CreatorScoreDto?> CalculateScoreAsync(int creatorId)
        {
            var creator = await _db.Creators.AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == creatorId);
            if (creator == null) return null;

            var analytics = await _db.CreatorAnalytics.AsNoTracking()
                .FirstOrDefaultAsync(a => a.CreatorId == creatorId);

            var growthPoints = await _db.CreatorGrowth.AsNoTracking()
                .Where(g => g.CreatorId == creatorId)
                .OrderBy(g => g.RecordedAt)
                .ToListAsync();

            var (engagementRate, avgViews, growthRate, uploadFreq) =
                ComputeRawInputs(creator, analytics, growthPoints);

            var (score, engComp, viewComp, growComp, freqComp) =
                ComputeScore(engagementRate, avgViews, growthRate, uploadFreq);

            // Persist (upsert)
            await UpsertScoreAsync(new CreatorScore
            {
                CreatorId            = creatorId,
                Score                = score,
                EngagementComponent  = engComp,
                ViewsComponent       = viewComp,
                GrowthComponent      = growComp,
                FrequencyComponent   = freqComp,
                EngagementRate       = engagementRate,
                AverageViews         = avgViews,
                SubscriberGrowthRate = growthRate,
                UploadFrequency      = uploadFreq,
                CalculatedAt         = DateTime.UtcNow
            });

            return BuildDto(creator, score, engComp, viewComp, growComp, freqComp,
                            engagementRate, avgViews, growthRate, uploadFreq);
        }

        public async Task RecalculateAllScoresAsync(CancellationToken ct = default)
        {
            var ids = await _db.Creators
                .Where(c => c.ChannelId != null && c.ChannelId != "")
                .Select(c => c.CreatorId)
                .ToListAsync(ct);
            _logger.LogInformation("Recalculating creator scores for {N} creators", ids.Count);
            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                try   { await CalculateScoreAsync(id); }
                catch (Exception ex)
                { _logger.LogWarning(ex, "Score calculation failed for creator {Id}", id); }
            }
        }

        public async Task<CreatorScoreDto?> GetScoreAsync(int creatorId)
        {
            var s = await _db.CreatorScores.AsNoTracking()
                .Include(x => x.Creator)
                .FirstOrDefaultAsync(x => x.CreatorId == creatorId);
            if (s == null) return null;

            return BuildDto(s.Creator, s.Score,
                s.EngagementComponent, s.ViewsComponent, s.GrowthComponent, s.FrequencyComponent,
                s.EngagementRate, s.AverageViews, s.SubscriberGrowthRate, s.UploadFrequency,
                s.CalculatedAt);
        }

        public async Task<CreatorComparisonDto> CompareCreatorsAsync(int creatorId1, int creatorId2)
        {
            var ids       = new[] { creatorId1, creatorId2 };
            var creators  = await _db.Creators.AsNoTracking()
                .Where(c => ids.Contains(c.CreatorId))
                .ToDictionaryAsync(c => c.CreatorId);

            var analytics = await _db.CreatorAnalytics.AsNoTracking()
                .Where(a => ids.Contains(a.CreatorId))
                .ToDictionaryAsync(a => a.CreatorId);

            var scores    = await _db.CreatorScores.AsNoTracking()
                .Where(s => ids.Contains(s.CreatorId))
                .ToDictionaryAsync(s => s.CreatorId);

            return new CreatorComparisonDto
            {
                Creator1 = BuildSide(creatorId1, creators, analytics, scores),
                Creator2 = BuildSide(creatorId2, creators, analytics, scores)
            };
        }

        // ────────────────────────────────────────────────────────────────────
        // Private helpers
        // ────────────────────────────────────────────────────────────────────

        private static (double eng, double views, double growth, double freq)
            ComputeRawInputs(
                Creator creator,
                CreatorAnalytics? analytics,
                List<CreatorGrowth> growthPoints)
        {
            double engagementRate = EngagementRateEstimator.EstimateOrStored(
                analytics?.EngagementRate,
                creator.Subscribers,
                creator.TotalViews,
                creator.VideoCount,
                analytics?.AvgViews);
            double avgViews       = analytics?.AvgViews
                                    ?? (creator.VideoCount > 0
                                        ? creator.TotalViews / (double)creator.VideoCount
                                        : 0);

            // Monthly subscriber growth rate
            double growthRate = 0;
            if (growthPoints.Count >= 2)
            {
                var oldest    = growthPoints.First();
                var newest    = growthPoints.Last();
                double months = (newest.RecordedAt - oldest.RecordedAt).TotalDays / 30.0;
                if (months >= 0.5 && oldest.Subscribers > 0)
                    growthRate = (newest.Subscribers - oldest.Subscribers)
                                 / (double)oldest.Subscribers / months;
                growthRate = Math.Max(0, growthRate);
            }

            // Upload frequency: videos per week
            double ageDays  = Math.Max((DateTime.UtcNow - creator.CreatedAt).TotalDays, 7);
            double uploadFreq = creator.VideoCount / (ageDays / 7.0);

            return (engagementRate, avgViews, growthRate, uploadFreq);
        }

        private static (double score, double eng, double views, double growth, double freq)
            ComputeScore(double engagementRate, double avgViews, double growthRate, double uploadFreq)
        {
            double normEng   = Math.Min(engagementRate / MaxEngagement,  1.0) * 100.0;
            double normViews = Math.Min(avgViews        / MaxAvgViews,   1.0) * 100.0;
            double normGrowth= Math.Min(growthRate      / MaxGrowth,     1.0) * 100.0;
            double normFreq  = Math.Min(uploadFreq      / MaxFrequency,  1.0) * 100.0;

            double engComp   = 0.4 * normEng;
            double viewComp  = 0.3 * normViews;
            double growComp  = 0.2 * normGrowth;
            double freqComp  = 0.1 * normFreq;

            double score     = Math.Round(engComp + viewComp + growComp + freqComp, 2);
            return (score,
                    Math.Round(engComp,  2),
                    Math.Round(viewComp, 2),
                    Math.Round(growComp, 2),
                    Math.Round(freqComp, 2));
        }

        private static CreatorScoreDto BuildDto(
            Creator creator,
            double score, double eng, double views, double growth, double freq,
            double engagementRate, double avgViews, double growthRate, double uploadFreq,
            DateTime? calculatedAt = null) =>
            new CreatorScoreDto
            {
                CreatorId            = creator.CreatorId,
                ChannelName          = creator.ChannelName  ?? string.Empty,
                Platform             = creator.Platform,
                Category             = creator.Category     ?? string.Empty,
                Country              = creator.Country      ?? string.Empty,
                Subscribers          = creator.Subscribers,
                Score                = score,
                EngagementComponent  = eng,
                ViewsComponent       = views,
                GrowthComponent      = growth,
                FrequencyComponent   = freq,
                EngagementRate       = engagementRate,
                AverageViews         = avgViews,
                SubscriberGrowthRate = growthRate,
                UploadFrequency      = uploadFreq,
                CalculatedAt         = calculatedAt ?? DateTime.UtcNow
            };

        private static CreatorComparisonSideDto BuildSide(
            int id,
            Dictionary<int, Creator> creators,
            Dictionary<int, CreatorAnalytics> analytics,
            Dictionary<int, CreatorScore> scores)
        {
            if (!creators.TryGetValue(id, out var c))
                return new CreatorComparisonSideDto { CreatorId = id };

            analytics.TryGetValue(id, out var a);
            scores.TryGetValue(id, out var s);

            double avgViews = a?.AvgViews
                              ?? (c.VideoCount > 0 ? c.TotalViews / (double)c.VideoCount : 0);
            double ageDays  = Math.Max((DateTime.UtcNow - c.CreatedAt).TotalDays, 7);
            double freq     = c.VideoCount / (ageDays / 7.0);

            string? breakdown = s == null ? null :
                $"Engagement {s.EngagementComponent:0.0} + " +
                $"Views {s.ViewsComponent:0.0} + " +
                $"Growth {s.GrowthComponent:0.0} + " +
                $"Frequency {s.FrequencyComponent:0.0}";

            return new CreatorComparisonSideDto
            {
                CreatorId       = c.CreatorId,
                ChannelName     = c.ChannelName  ?? string.Empty,
                Platform        = c.Platform,
                Category        = c.Category     ?? string.Empty,
                Subscribers     = c.Subscribers,
                AverageViews    = Math.Round(avgViews),
                EngagementRate  = EngagementRateEstimator.EstimateOrStored(
                    a?.EngagementRate,
                    c.Subscribers,
                    c.TotalViews,
                    c.VideoCount,
                    avgViews),
                UploadFrequency = Math.Round(freq, 2),
                CreatorScore    = s?.Score,
                ScoreBreakdown  = breakdown
            };
        }

        public async Task<PagedResultDto<CreatorScoreDto>> GetLeaderboardAsync(
            int page, int pageSize, string? category = null, string? country = null)
        {
            // ── 1. Load creators (with optional category + country filters) ──
            var creatorsQuery = _db.Creators.AsNoTracking().Where(c => c.ChannelId != null && c.ChannelId != "").AsQueryable();
            if (!string.IsNullOrWhiteSpace(category))
                creatorsQuery = creatorsQuery.Where(c => c.Category == category);
            // country filter: only apply when Country data has been populated
            if (!string.IsNullOrWhiteSpace(country))
                creatorsQuery = creatorsQuery.Where(c => c.Country == country);
            var creators = await creatorsQuery.ToListAsync();

            if (creators.Count == 0)
                return new PagedResultDto<CreatorScoreDto>
                {
                    Items = new(), TotalCount = 0, Page = page,
                    PageSize = pageSize, TotalPages = 0
                };

            // ── 2. Load analytics and cached scores in one shot ───────────────
            var ids = creators.Select(c => c.CreatorId).ToList();

            var analyticsMap = await _db.CreatorAnalytics.AsNoTracking()
                .Where(a => ids.Contains(a.CreatorId))
                .ToDictionaryAsync(a => a.CreatorId);

            // Growth points grouped by creator (needed for growth rate component)
            var growthMap = (await _db.CreatorGrowth.AsNoTracking()
                .Where(g => ids.Contains(g.CreatorId))
                .OrderBy(g => g.RecordedAt)
                .ToListAsync())
                .GroupBy(g => g.CreatorId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Prefer a persisted score if it already exists (avoids re-computing)
            var cachedScores = await _db.CreatorScores.AsNoTracking()
                .Where(s => ids.Contains(s.CreatorId))
                .ToDictionaryAsync(s => s.CreatorId);

            // ── 3. Score every creator in-memory ─────────────────────────────
            var scored = creators.Select(creator =>
            {
                analyticsMap.TryGetValue(creator.CreatorId, out var analytics);
                growthMap.TryGetValue(creator.CreatorId, out var growthPoints);

                if (cachedScores.TryGetValue(creator.CreatorId, out var cached))
                    return BuildDto(creator,
                        cached.Score,
                        cached.EngagementComponent, cached.ViewsComponent,
                        cached.GrowthComponent,     cached.FrequencyComponent,
                        cached.EngagementRate,      cached.AverageViews,
                        cached.SubscriberGrowthRate, cached.UploadFrequency,
                        cached.CalculatedAt);

                var (eng, views, growth, freq) =
                    ComputeRawInputs(creator, analytics, growthPoints ?? new List<CreatorGrowth>());
                var (score, engComp, viewComp, growComp, freqComp) =
                    ComputeScore(eng, views, growth, freq);

                return BuildDto(creator, score, engComp, viewComp, growComp, freqComp,
                                eng, views, growth, freq);
            })
            .OrderByDescending(d => d.Score)
            .ToList();

            // ── 4. Page the results ───────────────────────────────────────────
            var totalCount = scored.Count;
            var items = scored
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResultDto<CreatorScoreDto>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        private async Task UpsertScoreAsync(CreatorScore score)
        {
            var existing = await _db.CreatorScores
                .FirstOrDefaultAsync(s => s.CreatorId == score.CreatorId);

            if (existing == null)
            {
                _db.CreatorScores.Add(score);
            }
            else
            {
                existing.Score                = score.Score;
                existing.EngagementComponent  = score.EngagementComponent;
                existing.ViewsComponent       = score.ViewsComponent;
                existing.GrowthComponent      = score.GrowthComponent;
                existing.FrequencyComponent   = score.FrequencyComponent;
                existing.EngagementRate       = score.EngagementRate;
                existing.AverageViews         = score.AverageViews;
                existing.SubscriberGrowthRate = score.SubscriberGrowthRate;
                existing.UploadFrequency      = score.UploadFrequency;
                existing.CalculatedAt         = score.CalculatedAt;
                _db.CreatorScores.Update(existing);
            }

            await _db.SaveChangesAsync();
        }
    }
}
