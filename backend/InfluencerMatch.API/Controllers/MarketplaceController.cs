using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.API.Services;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Services;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.API.Controllers
{
    /// <summary>
    /// Brand-facing creator discovery marketplace.
    /// Shows both new Creator accounts (with linked channel) and legacy Influencer accounts.
    /// Legacy influencers use negative IDs (-InfluencerId) to distinguish them.
    ///
    ///   GET /api/marketplace/creators           — paginated, filterable list
    ///   GET /api/marketplace/creators/{id}      — full profile + recent videos
    /// </summary>
    [ApiController]
    [Route("api/marketplace")]
    public class MarketplaceController : ControllerBase
    {
        private readonly ApplicationDbContext   _db;
        private readonly ICreatorChannelService  _channelService;
        private readonly LegacyChannelCache      _legacyCache;

        public MarketplaceController(
            ApplicationDbContext    db,
            ICreatorChannelService  channelService,
            LegacyChannelCache      legacyCache)
        {
            _db             = db;
            _channelService = channelService;
            _legacyCache    = legacyCache;
        }

        /// <summary>
        /// Search registered creators with optional filters.
        /// Includes both Creator-role accounts (with YouTube channel) and legacy Influencer accounts.
        /// </summary>
        [HttpGet("creators")]
        [Authorize(Roles = "Brand,Agency,Individual,CreatorManager")]
        public async Task<IActionResult> SearchCreators(
            [FromQuery] MarketplaceSearchQueryDto query, CancellationToken ct)
        {
            // ── 1. New Creator system: CreatorChannels JOIN CreatorProfiles ──
            var creatorItems = await BuildCreatorQuery(query);

            // ── 2. Legacy Influencer system ──────────────────────────────────
            var influencerItems = await BuildInfluencerQuery(query, ct);

            // ── Merge, sort, paginate ─────────────────────────────────────────
            var all = creatorItems.Concat(influencerItems).ToList();

            all = (query.SortBy ?? "subscribers").ToLowerInvariant() switch
            {
                "engagement" => all.OrderByDescending(x => x.EngagementRate).ToList(),
                "views"      => all.OrderByDescending(x => x.TotalViews).ToList(),
                _            => all.OrderByDescending(x => x.Subscribers).ToList()
            };

            var total = all.Count;
            var skip  = (query.Page - 1) * query.PageSize;
            var items = all.Skip(skip).Take(query.PageSize).ToList();

            return Ok(new
            {
                Total    = total,
                Page     = query.Page,
                PageSize = query.PageSize,
                Items    = items
            });
        }

        /// <summary>Full creator profile detail including recent videos.</summary>
        [HttpGet("creators/{creatorProfileId:int}")]
        public async Task<IActionResult> GetCreatorDetail(
            int creatorProfileId,
            CancellationToken ct)
        {
            // Negative ID = legacy Influencer record
            if (creatorProfileId < 0)
                return await GetLegacyInfluencerDetail(-creatorProfileId, ct);

            var row = await _db.CreatorChannels
                .Join(_db.CreatorProfiles,
                      ch => ch.CreatorProfileId,
                      pr => pr.CreatorProfileId,
                      (ch, pr) => new { ch, pr })
                .FirstOrDefaultAsync(x => x.pr.CreatorProfileId == creatorProfileId, ct);

            if (row == null)
                return NotFound(new { error = "Creator not found in marketplace." });

            var recentVideos = await _channelService
                .GetRecentVideosAsync(row.ch.ChannelId, 10, ct);

            // Resolve legacy Creator.CreatorId via ChannelId (populated by Discovery job)
            var creatorId = await _db.Creators
                .Where(c => c.ChannelId == row.ch.ChannelId)
                .Select(c => (int?)c.CreatorId)
                .FirstOrDefaultAsync(ct);

            var detailEngagement = row.ch.EngagementRate > 0
                ? EngagementRateEstimator.Clamp(row.ch.EngagementRate)
                : EstimateFromVideoRows(row.ch.Subscribers, recentVideos);

            return Ok(new MarketplaceCreatorDetailDto
            {
                CreatorProfileId = row.pr.CreatorProfileId,
                CreatorId        = creatorId,
                ChannelId        = row.ch.ChannelId,
                ChannelName      = row.ch.ChannelName,
                ThumbnailUrl     = row.ch.ThumbnailUrl,
                Subscribers      = row.ch.Subscribers,
                TotalViews       = row.ch.TotalViews,
                EngagementRate   = detailEngagement,
                CreatorTier      = row.ch.CreatorTier,
                Language         = row.pr.Language,
                Category         = row.pr.Category,
                Country          = row.pr.Country,
                IsVerified       = row.ch.IsVerified,
                ContactEmail     = row.pr.ContactEmail,
                Description      = row.ch.Description,
                InstagramHandle  = row.pr.InstagramHandle,
                Bio              = row.pr.Bio,
                RecentVideos     = recentVideos
            });
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task<List<MarketplaceCreatorDto>> BuildCreatorQuery(
            MarketplaceSearchQueryDto query)
        {
            var q = _db.CreatorChannels
                .Join(_db.CreatorProfiles,
                      ch => ch.CreatorProfileId,
                      pr => pr.CreatorProfileId,
                      (ch, pr) => new { ch, pr })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
                q = q.Where(x => x.ch.ChannelName.Contains(query.Search));
            if (!string.IsNullOrWhiteSpace(query.Language))
                q = q.Where(x => x.pr.Language == query.Language);
            if (!string.IsNullOrWhiteSpace(query.Category))
                q = q.Where(x => x.pr.Category == query.Category);
            if (!string.IsNullOrWhiteSpace(query.Country))
                q = q.Where(x => x.pr.Country == query.Country);
            if (!string.IsNullOrWhiteSpace(query.Region))
                q = q.Where(x => x.pr.Country == query.Region);
            if (!string.IsNullOrWhiteSpace(query.CreatorTier))
                q = q.Where(x => x.ch.CreatorTier == query.CreatorTier);
            if (query.MinSubscribers.HasValue)
                q = q.Where(x => x.ch.Subscribers >= query.MinSubscribers.Value);
            if (query.MaxSubscribers.HasValue)
                q = q.Where(x => x.ch.Subscribers <= query.MaxSubscribers.Value);
            var records = await q.ToListAsync();
            var items = records.Select(x => new MarketplaceCreatorDto
            {
                CreatorProfileId = x.pr.CreatorProfileId,
                ChannelId        = x.ch.ChannelId,
                ChannelName      = x.ch.ChannelName,
                ThumbnailUrl     = x.ch.ThumbnailUrl,
                Subscribers      = x.ch.Subscribers,
                TotalViews       = x.ch.TotalViews,
                EngagementRate   = EngagementRateEstimator.EstimateOrStored(
                    x.ch.EngagementRate,
                    x.ch.Subscribers,
                    x.ch.TotalViews,
                    x.ch.VideoCount),
                CreatorTier      = x.ch.CreatorTier,
                Language         = x.pr.Language,
                Category         = x.pr.Category,
                Country          = x.pr.Country,
                IsVerified       = x.ch.IsVerified,
                ContactEmail     = x.pr.ContactEmail
            }).ToList();

            if (query.MinEngagement.HasValue)
                items = items.Where(x => x.EngagementRate >= query.MinEngagement.Value).ToList();

            return items;
        }

        private async Task<List<MarketplaceCreatorDto>> BuildInfluencerQuery(
            MarketplaceSearchQueryDto query, CancellationToken ct = default)
        {
            var q = _db.Influencers
                .Join(_db.Users,
                      i => i.UserId,
                      u => u.UserId,
                      (i, u) => new { i, u })
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
                q = q.Where(x => x.u.Name.Contains(query.Search));
            if (!string.IsNullOrWhiteSpace(query.Category))
                q = q.Where(x => x.i.Category == query.Category);
            if (!string.IsNullOrWhiteSpace(query.Country))
                q = q.Where(x => x.i.Location == query.Country);
            if (query.MinSubscribers.HasValue)
                q = q.Where(x => x.i.Followers >= query.MinSubscribers.Value);
            if (query.MaxSubscribers.HasValue)
                q = q.Where(x => x.i.Followers <= query.MaxSubscribers.Value);
            // Unsupported filters for legacy: Language, CreatorTier
            if (!string.IsNullOrWhiteSpace(query.Language) || !string.IsNullOrWhiteSpace(query.CreatorTier))
                return new List<MarketplaceCreatorDto>();

            var records = await q.ToListAsync();

            // Read from SuperAdmin-populated cache — no live YouTube calls on page load
            return records.Select(x =>
            {
                var live = !string.IsNullOrWhiteSpace(x.i.YouTubeLink)
                    ? _legacyCache.GetSnapshot(x.i.YouTubeLink!)
                    : null;

                var engagementRate = EngagementRateEstimator.EstimateOrStored(
                    x.i.EngagementRate,
                    live?.Subscribers ?? x.i.Followers,
                    live?.TotalViews ?? 0,
                    live?.VideoCount ?? 0);

                return new MarketplaceCreatorDto
                {
                    // Negative ID signals "legacy influencer" to the detail endpoint
                    CreatorProfileId = -x.i.InfluencerId,
                    ChannelId        = live?.ChannelId   ?? x.i.YouTubeLink ?? string.Empty,
                    ChannelName      = live?.ChannelName ?? x.u.Name,
                    ThumbnailUrl     = live?.ThumbnailUrl,
                    Subscribers      = live?.Subscribers  ?? x.i.Followers,
                    TotalViews       = live?.TotalViews   ?? 0,
                    EngagementRate   = engagementRate,
                    CreatorTier      = live?.CreatorTier  ?? TierFromFollowers(x.i.Followers),
                    Language         = null,
                    Category         = x.i.Category,
                    Country          = live?.Country ?? x.i.Location,
                    IsVerified       = false,
                    ContactEmail     = x.u.Email
                };
            })
            .Where(x => !query.MinEngagement.HasValue || x.EngagementRate >= query.MinEngagement.Value)
            .ToList();
        }

        private async Task<IActionResult> GetLegacyInfluencerDetail(
            int influencerId, CancellationToken ct)
        {
            var row = await _db.Influencers
                .Join(_db.Users,
                      i => i.UserId,
                      u => u.UserId,
                      (i, u) => new { i, u })
                .FirstOrDefaultAsync(x => x.i.InfluencerId == influencerId, ct);

            if (row == null)
                return NotFound(new { error = "Influencer not found." });

            // Read from SuperAdmin-populated cache — no live YouTube calls on detail load
            LiveChannelSnapshot? live = !string.IsNullOrWhiteSpace(row.i.YouTubeLink)
                ? _legacyCache.GetSnapshot(row.i.YouTubeLink!)
                : null;

            // Videos are also cached by the same SuperAdmin job
            var recentVideos = live != null
                ? _legacyCache.GetVideos(live.ChannelId)
                : new List<ChannelVideoDto>();

            // Resolve legacy Creator.CreatorId via ChannelId if the discovery job has run
            var channelIdForLookup = live?.ChannelId ?? row.i.YouTubeLink ?? string.Empty;
            var legacyCreatorId = !string.IsNullOrEmpty(channelIdForLookup)
                ? await _db.Creators
                    .Where(c => c.ChannelId == channelIdForLookup)
                    .Select(c => (int?)c.CreatorId)
                    .FirstOrDefaultAsync(ct)
                : null;

            var detailEngagement = EngagementRateEstimator.EstimateOrStored(
                row.i.EngagementRate,
                live?.Subscribers ?? row.i.Followers,
                live?.TotalViews ?? 0,
                live?.VideoCount ?? 0);

            return Ok(new MarketplaceCreatorDetailDto
            {
                CreatorProfileId = -row.i.InfluencerId,
                CreatorId        = legacyCreatorId,
                ChannelId        = live?.ChannelId   ?? row.i.YouTubeLink ?? string.Empty,
                ChannelName      = live?.ChannelName ?? row.u.Name,
                ThumbnailUrl     = live?.ThumbnailUrl,
                Subscribers      = live?.Subscribers  ?? row.i.Followers,
                TotalViews       = live?.TotalViews   ?? 0,
                EngagementRate   = detailEngagement,
                CreatorTier      = live?.CreatorTier  ?? TierFromFollowers(row.i.Followers),
                Language         = null,
                Category         = row.i.Category,
                Country          = live?.Country ?? row.i.Location,
                IsVerified       = false,
                ContactEmail     = row.u.Email,
                Description      = live?.Description ?? $"YouTube: {row.i.YouTubeLink}",
                InstagramHandle  = row.i.InstagramLink,
                Bio              = null,
                RecentVideos     = recentVideos
            });
        }

        private static string TierFromFollowers(long followers) => followers switch
        {
            >= 1_000_000 => "Mega",
            >= 500_000   => "Macro",
            >= 100_000   => "MidTier",
            >= 10_000    => "Micro",
            _            => "Nano"
        };

        private static double EstimateFromVideoRows(long subscribers, List<ChannelVideoDto> videos)
        {
            if (videos == null || videos.Count == 0)
                return EngagementRateEstimator.EstimateFromAverages(subscribers, 0);

            var withViews = videos.Where(v => v.ViewCount > 0).ToList();
            if (withViews.Count == 0)
                return EngagementRateEstimator.EstimateFromAverages(subscribers, 0);

            var ratios = withViews
                .Select(v => (v.LikeCount + v.CommentCount) / (double)v.ViewCount)
                .Where(r => double.IsFinite(r) && r > 0)
                .ToList();

            if (ratios.Count == 0)
                return EngagementRateEstimator.EstimateFromAverages(subscribers, withViews.Average(v => (double)v.ViewCount));

            return EngagementRateEstimator.Clamp(ratios.Average());
        }
    }
}
