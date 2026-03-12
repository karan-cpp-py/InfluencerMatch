using System;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

[ApiController]
[Route("api/[controller]")]
public class DiscoveryController : ControllerBase
{
    private readonly IYouTubeQuotaTracker  _quota;
    private readonly ApplicationDbContext  _db;
    private readonly IAdvancedAnalyticsService _advancedAnalytics;
    private readonly IConfiguration        _config;

    public DiscoveryController(
        IYouTubeQuotaTracker  quota,
        ApplicationDbContext  db,
        IAdvancedAnalyticsService advancedAnalytics,
        IConfiguration        config)
    {
        _quota  = quota;
        _db     = db;
        _advancedAnalytics = advancedAnalytics;
        _config = config;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var apiKey            = _config["YouTube:ApiKey"];
        var youtubeConfigured = IsApiKeyConfigured(apiKey);
        var total             = await _db.Creators.CountAsync(c => c.UserId != null);
        var smallCreators     = await _db.Creators.CountAsync(c => c.UserId != null && c.IsSmallCreator);
        var today             = DateTime.UtcNow.Date;
        var addedToday        = await _db.Creators.CountAsync(c => c.UserId != null && c.CreatedAt >= today);
        var topCategories     = await _db.Creators
            .Where(c => c.UserId != null && c.Category != null && c.Category != "")
            .GroupBy(c => c.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => new { category = g.Key, count = g.Count() })
            .Take(5)
            .ToListAsync();
        var tierBreakdown = await _db.Creators
            .Where(c => c.UserId != null && c.CreatorTier != null)
            .GroupBy(c => c.CreatorTier)
            .Select(g => new { tier = g.Key, count = g.Count() })
            .ToListAsync();
        var totalSubscribers = await _db.Creators
            .Where(c => c.UserId != null)
            .SumAsync(c => (long?)c.Subscribers) ?? 0;
        return Ok(new
        {
            total,
            smallCreators,
            addedToday,
            topCategories,
            tierBreakdown,
            youtubeConfigured,
            totalSubscribers,
            quota = new
            {
                usedToday  = _quota.UsedToday,
                dailyLimit = _quota.DailyLimit,
                remaining  = _quota.Remaining
            }
        });
    }

    [HttpGet("creators")]
    public async Task<IActionResult> GetCreators(
        [FromQuery] string? search   = null,
        [FromQuery] string? category = null,
        [FromQuery] string  sort     = "subscribers",
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        // Only show registered creators
        var query = _db.Creators.Where(c => c.UserId != null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.ChannelName != null && c.ChannelName.ToLower().Contains(search.ToLower()));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);

        query = sort switch
        {
            "views"  => query.OrderByDescending(c => c.TotalViews),
            "videos" => query.OrderByDescending(c => c.VideoCount),
            "newest" => query.OrderByDescending(c => c.CreatedAt),
            _        => query.OrderByDescending(c => c.Subscribers)
        };

        var totalCount = await query.CountAsync();
        var creators = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new {
                c.CreatorId,
                c.ChannelId,
                c.ChannelName,
                c.Platform,
                c.Subscribers,
                c.TotalViews,
                c.VideoCount,
                c.Category,
                c.Country,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(new {
            creators,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    // -- Internal / AI-training endpoint --

    /// <summary>
    /// Returns ALL registered creators (with UserId) regardless of subscriber count.
    /// Intended for internal analytics dashboards and AI model training pipelines.
    /// </summary>
    [HttpGet("/api/internal/creators")]
    public async Task<IActionResult> GetAllCreatorsInternal(
        [FromQuery] string? search   = null,
        [FromQuery] string? category = null,
        [FromQuery] string? tier     = null,
        [FromQuery] string? country  = null,
        [FromQuery] long?   minSubs  = null,
        [FromQuery] long?   maxSubs  = null,
        [FromQuery] string  sort     = "subscribers",
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 50)
    {
        if (pageSize > 200) pageSize = 200;

        // Only registered creators
        var query = _db.Creators.Where(c => c.UserId != null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.ChannelName != null && c.ChannelName.ToLower().Contains(search.ToLower()));
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);
        if (!string.IsNullOrWhiteSpace(tier))
            query = query.Where(c => c.CreatorTier == tier);
        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(c => c.Country == country);
        if (minSubs.HasValue)
            query = query.Where(c => c.Subscribers >= minSubs.Value);
        if (maxSubs.HasValue)
            query = query.Where(c => c.Subscribers <= maxSubs.Value);

        query = sort switch
        {
            "views"  => query.OrderByDescending(c => c.TotalViews),
            "videos" => query.OrderByDescending(c => c.VideoCount),
            "newest" => query.OrderByDescending(c => c.CreatedAt),
            "tier"   => query.OrderBy(c => c.CreatorTier),
            _        => query.OrderByDescending(c => c.Subscribers)
        };

        var totalCount = await query.CountAsync();
        var creators = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.CreatorId,
                c.ChannelId,
                c.ChannelName,
                c.Description,
                c.Platform,
                c.Subscribers,
                c.TotalViews,
                c.VideoCount,
                c.Category,
                c.Country,
                c.CreatorTier,
                c.IsSmallCreator,
                c.Language,
                c.Region,
                c.CreatedAt,
                c.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            creators,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.Creators
            .Where(c => c.UserId != null && c.Category != null && c.Category != "")
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        return Ok(categories);
    }

    private static bool IsApiKeyConfigured(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;
        if (apiKey.Contains("YOUR_YOUTUBE", StringComparison.OrdinalIgnoreCase)) return false;
        if (apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase)) return false;
        if (apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    // ── Brand-facing: imported YouTube creators ──────────────────────────────

    /// <summary>
    /// Returns YouTube creators imported by SuperAdmin (UserId == null).
    /// Paginated and filterable. Accessible to any authenticated brand/user.
    /// GET /api/discovery/youtube-creators
    /// </summary>
    [HttpGet("youtube-creators")]
    public async Task<IActionResult> GetYouTubeCreators(
        [FromQuery] string? search        = null,
        [FromQuery] string? category      = null,
        [FromQuery] string? tier          = null,
        [FromQuery] string? country       = null,
        [FromQuery] long?   minSubs       = null,
        [FromQuery] long?   maxSubs       = null,
        [FromQuery] double? minEngagement = null,
        [FromQuery] string  sort          = "subscribers",
        [FromQuery] int     page          = 1,
        [FromQuery] int     pageSize      = 20)
    {
        if (pageSize > 100) pageSize = 100;

        // Imported creators: UserId is null
        var query = _db.Creators.Where(c => c.UserId == null);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.ChannelName != null &&
                EF.Functions.ILike(c.ChannelName, $"%{search}%"));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);

        if (!string.IsNullOrWhiteSpace(tier))
            query = query.Where(c => c.CreatorTier == tier);

        if (!string.IsNullOrWhiteSpace(country))
            query = query.Where(c => c.Country == country);

        if (minSubs.HasValue)
            query = query.Where(c => c.Subscribers >= minSubs.Value);

        if (maxSubs.HasValue)
            query = query.Where(c => c.Subscribers <= maxSubs.Value);

        if (minEngagement.HasValue)
            query = query.Where(c => c.EngagementRate >= minEngagement.Value);

        query = sort switch
        {
            "engagement" => query.OrderByDescending(c => c.EngagementRate),
            "views"      => query.OrderByDescending(c => c.TotalViews),
            "newest"     => query.OrderByDescending(c => c.LastRefreshedAt),
            _            => query.OrderByDescending(c => c.Subscribers)
        };

        var totalCount = await query.CountAsync();
        var creators = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new {
                c.CreatorId,
                c.ChannelId,
                c.ChannelName,
                c.ThumbnailUrl,
                c.ChannelUrl,
                c.Category,
                c.Country,
                c.CreatorTier,
                c.Subscribers,
                c.TotalViews,
                c.VideoCount,
                c.AvgViews,
                c.AvgLikes,
                c.AvgComments,
                c.EngagementRate,
                c.PublicEmail,
                c.InstagramHandle,
                c.TwitterHandle,
                c.ChannelTags,
                c.PublishedAt,
                c.LastRefreshedAt,
            })
            .ToListAsync();

        return Ok(new {
            creators,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Returns full profile for one imported YouTube creator including recent videos.
    /// GET /api/discovery/youtube-creators/{creatorId}
    /// </summary>
    [HttpGet("youtube-creators/{creatorId:int}")]
    public async Task<IActionResult> GetYouTubeCreatorDetail(int creatorId)
    {
        var creator = await _db.Creators
            .Where(c => c.CreatorId == creatorId && c.UserId == null)
            .Select(c => new {
                c.CreatorId,
                c.ChannelId,
                c.ChannelName,
                c.Description,
                c.ThumbnailUrl,
                c.BannerUrl,
                c.ChannelUrl,
                c.Category,
                c.Country,
                c.CreatorTier,
                c.Subscribers,
                c.TotalViews,
                c.VideoCount,
                c.AvgViews,
                c.AvgLikes,
                c.AvgComments,
                c.EngagementRate,
                c.PublicEmail,
                c.InstagramHandle,
                c.TwitterHandle,
                c.ChannelTags,
                c.PublishedAt,
                c.LastRefreshedAt,
            })
            .FirstOrDefaultAsync();

        if (creator == null) return NotFound();

        var videos = await _db.Videos
            .Where(v => v.CreatorId == creatorId)
            .OrderByDescending(v => v.PublishedAt)
            .Take(10)
            .Select(v => new {
                v.VideoId,
                v.Title,
                v.ThumbnailUrl,
                v.ViewCount,
                v.LikeCount,
                v.CommentCount,
                v.EngagementRate,
                v.PublishedAt,
                v.Tags,
            })
            .ToListAsync();

        return Ok(new { creator, videos });
    }

    /// <summary>
    /// Returns distinct categories for all imported YouTube creators.
    /// GET /api/discovery/youtube-creators/categories
    /// </summary>
    [HttpGet("youtube-creators/categories")]
    public async Task<IActionResult> GetYouTubeCreatorCategories()
    {
        var cats = await _db.Creators
            .Where(c => c.UserId == null && c.Category != null && c.Category != "")
            .Select(c => c.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
        return Ok(cats);
    }

    /// <summary>
    /// Advanced creator analytics bundle for brand evaluation:
    /// health scorecard, audience quality, and creator coaching snapshot.
    /// </summary>
    [HttpGet("youtube-creators/{creatorId:int}/insights")]
    public async Task<IActionResult> GetYouTubeCreatorInsights(
        int creatorId,
        [FromQuery] string? brandCategory = null,
        [FromQuery] string? brandCountry = null,
        [FromQuery] string? brandLanguage = null)
    {
        var insights = await _advancedAnalytics.GetCreatorInsightsAsync(
            creatorId,
            brandCategory,
            brandCountry,
            brandLanguage);

        if (insights == null) return NotFound();
        return Ok(insights);
    }

    /// <summary>
    /// Creator-brand fit analytics for shortlisting decisions.
    /// </summary>
    [HttpGet("youtube-creators/{creatorId:int}/fit")]
    public async Task<IActionResult> GetYouTubeCreatorFit(
        int creatorId,
        [FromQuery] string? brandCategory = null,
        [FromQuery] string? brandCountry = null,
        [FromQuery] string? brandLanguage = null)
    {
        var fit = await _advancedAnalytics.GetCreatorBrandFitAsync(
            creatorId,
            brandCategory,
            brandCountry,
            brandLanguage);

        if (fit == null) return NotFound();
        return Ok(fit);
    }

    [HttpGet("opportunity-radar")]
    public async Task<IActionResult> GetOpportunityRadar(
        [FromQuery] string? category = null,
        [FromQuery] string? country = null,
        [FromQuery] string? language = null,
        [FromQuery] int limit = 10)
    {
        var result = await _advancedAnalytics.GetOpportunityRadarAsync(category, country, language, limit);
        return Ok(result);
    }

    [HttpGet("youtube-creators/{creatorId:int}/readiness")]
    public async Task<IActionResult> GetSponsorshipReadiness(int creatorId)
    {
        var result = await _advancedAnalytics.GetSponsorshipReadinessAsync(creatorId);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("regional-language-performance")]
    public async Task<IActionResult> GetRegionalLanguagePerformance(
        [FromQuery] string? category = null,
        [FromQuery] string? country = null,
        [FromQuery] string? brandLanguage = null)
    {
        var result = await _advancedAnalytics.GetRegionalLanguagePerformanceAsync(category, country, brandLanguage);
        return Ok(result);
    }
}
