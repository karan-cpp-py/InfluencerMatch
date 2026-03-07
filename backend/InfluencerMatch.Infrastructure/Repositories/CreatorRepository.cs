using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Repositories
{
    public class CreatorRepository : Repository<Creator>, ICreatorRepository
    {
        public CreatorRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Creator?> GetByChannelIdAsync(string channelId) =>
            await _context.Creators.FirstOrDefaultAsync(c => c.ChannelId == channelId);

        public async Task<List<Creator>> GetAllWithUsersAsync() =>
            await _context.Creators
                .AsNoTracking()
                .Include(c => c.User)
                .ToListAsync();

        public async Task<Dictionary<int, CreatorAnalytics>> GetLatestAnalyticsMapAsync(IEnumerable<int> creatorIds)
        {
            var ids = creatorIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, CreatorAnalytics>();
            }

            return await _context.CreatorAnalytics
                .AsNoTracking()
                .Where(a => ids.Contains(a.CreatorId))
                .ToDictionaryAsync(a => a.CreatorId);
        }

        public async Task<List<Creator>> GetAllWithAnalyticsAsync() =>
            await _context.Creators
                .AsNoTracking()
                .Include(c => _context.CreatorAnalytics.Where(a => a.CreatorId == c.CreatorId))
                .ToListAsync();

        public async Task<PagedResultDto<CreatorSearchResultDto>> SearchAsync(CreatorSearchQueryDto q)
        {
            // Left join creators (registered only) with their latest analytics
            var query = from c in _context.Creators.Where(c => c.UserId != null)
                        join a in _context.CreatorAnalytics on c.CreatorId equals a.CreatorId into aj
                        from a in aj.DefaultIfEmpty()
                        select new { c, a };

            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.c.ChannelName != null &&
                                         x.c.ChannelName.ToLower().Contains(q.Search.ToLower()));

            if (!string.IsNullOrWhiteSpace(q.Category))
                query = query.Where(x => x.c.Category == q.Category);

            if (!string.IsNullOrWhiteSpace(q.Platform))
                query = query.Where(x => x.c.Platform == q.Platform);

            if (!string.IsNullOrWhiteSpace(q.Country))
                query = query.Where(x => x.c.Country == q.Country);

            if (!string.IsNullOrWhiteSpace(q.Language))
                query = query.Where(x => x.c.Language == q.Language);

            if (!string.IsNullOrWhiteSpace(q.Region))
                query = query.Where(x => x.c.Region == q.Region);

            if (!string.IsNullOrWhiteSpace(q.CreatorTier))
                query = query.Where(x => x.c.CreatorTier == q.CreatorTier);

            // Default: restrict public search to small creators (Nano/Micro/MidTier, 1K–500K subs)
            if (q.OnlySmallCreators)
            {
                if (!q.MinSubscribers.HasValue)
                    query = query.Where(x => x.c.Subscribers >= 1_000);
                if (!q.MaxSubscribers.HasValue)
                    query = query.Where(x => x.c.Subscribers <= 500_000);
            }

            if (q.MinSubscribers.HasValue)
                query = query.Where(x => x.c.Subscribers >= q.MinSubscribers.Value);

            if (q.MaxSubscribers.HasValue)
                query = query.Where(x => x.c.Subscribers <= q.MaxSubscribers.Value);

            if (q.MinEngagement.HasValue)
                query = query.Where(x => x.a != null && x.a.EngagementRate >= q.MinEngagement.Value);

            if (q.MaxEngagement.HasValue)
                query = query.Where(x => x.a != null && x.a.EngagementRate <= q.MaxEngagement.Value);

            query = q.SortBy?.ToLowerInvariant() switch
            {
                "views"       => query.OrderByDescending(x => x.c.TotalViews),
                "engagement"  => query.OrderByDescending(x => x.a != null ? x.a.EngagementRate : 0),
                "videos"      => query.OrderByDescending(x => x.c.VideoCount),
                "newest"      => query.OrderByDescending(x => x.c.CreatedAt),
                _             => query.OrderByDescending(x => x.c.Subscribers)
            };

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(x => new CreatorSearchResultDto
                {
                    CreatorId               = x.c.CreatorId,
                    ChannelId               = x.c.ChannelId,
                    ChannelName             = x.c.ChannelName ?? string.Empty,
                    Platform                = x.c.Platform,
                    Category                = x.c.Category ?? string.Empty,
                    Country                 = x.c.Country ?? string.Empty,
                    Subscribers             = x.c.Subscribers,
                    TotalViews              = x.c.TotalViews,
                    VideoCount              = x.c.VideoCount,
                    EngagementRate          = x.a != null ? x.a.EngagementRate : 0,
                    AvgViews                = x.a != null ? x.a.AvgViews       : 0,
                    AvgLikes                = x.a != null ? x.a.AvgLikes       : 0,
                    CreatorTier             = x.c.CreatorTier,
                    IsSmallCreator          = x.c.IsSmallCreator,
                    Language                = x.c.Language,
                    Region                  = x.c.Region,
                    LanguageConfidenceScore = x.c.LanguageConfidenceScore
                })
                .ToListAsync();

            return new PagedResultDto<CreatorSearchResultDto>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = q.Page,
                PageSize   = q.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)q.PageSize)
            };
        }

        public async Task UpsertAnalyticsAsync(CreatorAnalytics analytics)
        {
            var existing = await _context.CreatorAnalytics
                .FirstOrDefaultAsync(a => a.CreatorId == analytics.CreatorId);

            if (existing == null)
            {
                _context.CreatorAnalytics.Add(analytics);
            }
            else
            {
                existing.AvgViews       = analytics.AvgViews;
                existing.AvgLikes       = analytics.AvgLikes;
                existing.AvgComments    = analytics.AvgComments;
                existing.EngagementRate = analytics.EngagementRate;
                existing.CalculatedAt   = analytics.CalculatedAt;
                _context.CreatorAnalytics.Update(existing);
            }

            await _context.SaveChangesAsync();
        }

        public async Task AddGrowthSnapshotAsync(CreatorGrowth growth)
        {
            _context.CreatorGrowth.Add(growth);
            await _context.SaveChangesAsync();
        }

        public async Task<List<CreatorGrowth>> GetGrowthHistoryAsync(int creatorId, int maxPoints = 30) =>
            await _context.CreatorGrowth
                .AsNoTracking()
                .Where(g => g.CreatorId == creatorId)
                .OrderByDescending(g => g.RecordedAt)
                .Take(maxPoints)
                .ToListAsync();

        public async Task<CreatorAnalytics?> GetLatestAnalyticsAsync(int creatorId) =>
            await _context.CreatorAnalytics
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.CreatorId == creatorId);
    }
}
