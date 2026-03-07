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
    private readonly IConfiguration        _config;

    public DiscoveryController(
        IYouTubeQuotaTracker  quota,
        ApplicationDbContext  db,
        IConfiguration        config)
    {
        _quota  = quota;
        _db     = db;
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
}
