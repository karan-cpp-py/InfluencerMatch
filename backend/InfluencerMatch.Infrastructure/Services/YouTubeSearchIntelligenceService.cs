using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Services
{
    public class YouTubeSearchIntelligenceService : IYouTubeSearchIntelligenceService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHuggingFaceNlpService _nlp;
        private readonly IGroqLlmService _groq;
        private readonly IHttpClientFactory _http;
        private readonly IYouTubeQuotaTracker _quota;
        private readonly string? _apiKey;

        private static readonly string[] StopWords =
        {
            "the", "and", "for", "with", "from", "that", "this", "you", "your", "have", "about", "into", "video", "channel"
        };

        public YouTubeSearchIntelligenceService(
            ApplicationDbContext db,
            IHuggingFaceNlpService nlp,
            IGroqLlmService groq,
            IHttpClientFactory http,
            IYouTubeQuotaTracker quota,
            IConfiguration configuration)
        {
            _db = db;
            _nlp = nlp;
            _groq = groq;
            _http = http;
            _quota = quota;
            _apiKey = configuration["YouTube:ApiKey"]
                   ?? configuration["YouTube__ApiKey"]
                   ?? configuration["YOUTUBE_API_KEY"];
        }

        public async Task<YouTubeSearchResultDto> SearchAsync(YouTubeSearchQueryRequestDto request, CancellationToken ct = default)
        {
            var limit = Math.Clamp(request.Limit <= 0 ? 20 : request.Limit, 1, 50);
            var queryText = (request.Query ?? string.Empty).Trim();
            var lowered = queryText.ToLowerInvariant();
            var tokens = Tokenize(queryText);

            var intent = await BuildIntentAsync(queryText, ct);

            var liveResults = await SearchLiveYouTubeChannelsAsync(request, limit, tokens, lowered, intent.TopLabel, ct);
            if (liveResults.Count > 0)
            {
                var aiBriefLive = await BuildAiSearchBriefAsync(queryText, intent.TopLabel, liveResults);
                return new YouTubeSearchResultDto
                {
                    Query = queryText,
                    IntentLabel = intent.TopLabel,
                    IntentReason = "Live YouTube Search API + NLP reranking",
                    AiSearchBrief = aiBriefLive,
                    Results = liveResults
                };
            }
            return new YouTubeSearchResultDto
            {
                Query = queryText,
                IntentLabel = intent.TopLabel,
                IntentReason = IsApiKeyConfigured()
                    ? "Strict live YouTube Search API mode: no matching channels returned"
                    : "Strict live YouTube Search API mode: API key not configured",
                AiSearchBrief = null,
                Results = new List<YouTubeSearchCreatorDto>()
            };
        }

        public async Task<YouTubeCreatorAnalysisResponseDto?> AnalyzeCreatorAsync(YouTubeCreatorAnalysisRequestDto request, CancellationToken ct = default)
        {
            if (request.CreatorId <= 0 && !string.IsNullOrWhiteSpace(request.ChannelId))
            {
                return await AnalyzeByChannelIdAsync(request.ChannelId.Trim(), request.Mode, request.SearchContext, ct);
            }

            var creator = await _db.Creators
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CreatorId == request.CreatorId, ct);

            if (creator == null)
                return null;

            var mode = NormalizeMode(request.Mode);
            var take = mode switch
            {
                "last20" => 20,
                "channel" => 1000,
                _ => 10
            };

            var videos = await _db.Videos
                .AsNoTracking()
                .Where(v => v.CreatorId == request.CreatorId)
                .OrderByDescending(v => v.PublishedAt)
                .Take(take)
                .ToListAsync(ct);

            var useEstimated = videos.Count == 0;
            var avgViews = useEstimated ? EstimateAvgViews(creator.TotalViews, creator.VideoCount) : videos.Average(v => v.ViewCount);
            var avgLikes = useEstimated ? avgViews * 0.032 : videos.Average(v => v.LikeCount);
            var avgComments = useEstimated ? avgViews * 0.005 : videos.Average(v => v.CommentCount);
            var avgEngagement = useEstimated
                ? NormalizeEngagement(creator.EngagementRate)
                : Math.Clamp(videos.Average(v => NormalizeEngagement(v.EngagementRate)), 0.0, 1.0);

            var topKeywords = ExtractTopKeywords(videos, creator).Take(8).ToList();
            var momentum = BuildMomentumScore(videos, avgEngagement, creator.Subscribers);

            var fit = await BuildCampaignFitAsync(topKeywords, creator.Category, request.SearchContext);

            var aiNarrative = await BuildAiNarrativeAsync(
                creator.ChannelName,
                mode,
                videos.Count,
                avgViews,
                avgEngagement,
                momentum,
                fit,
                topKeywords,
                request.SearchContext);

            return new YouTubeCreatorAnalysisResponseDto
            {
                CreatorId = creator.CreatorId,
                ChannelName = creator.ChannelName,
                Mode = mode,
                VideosAnalyzed = videos.Count,
                FromDateUtc = videos.Count > 0 ? videos.Min(v => v.PublishedAt) : null,
                ToDateUtc = videos.Count > 0 ? videos.Max(v => v.PublishedAt) : null,
                AverageViews = Math.Round(avgViews, 2),
                AverageLikes = Math.Round(avgLikes, 2),
                AverageComments = Math.Round(avgComments, 2),
                AverageEngagementRate = Math.Round(avgEngagement, 4),
                MomentumScore = momentum,
                CampaignFitLabel = fit,
                DataQuality = useEstimated ? "Estimated from channel-level data" : "Direct video sample",
                AiNarrative = aiNarrative,
                TopKeywords = topKeywords,
                TopVideos = videos
                    .OrderByDescending(v => v.ViewCount)
                    .Take(5)
                    .Select(v => new YouTubeAnalysisVideoDto
                    {
                        VideoId = v.VideoId,
                        Title = v.Title,
                        ViewCount = v.ViewCount,
                        LikeCount = v.LikeCount,
                        CommentCount = v.CommentCount,
                        EngagementRate = NormalizeEngagement(v.EngagementRate),
                        PublishedAt = v.PublishedAt
                    })
                    .ToList(),
                ActionPlan = BuildActionPlan(fit, avgEngagement, momentum, topKeywords)
            };
        }

        public async Task<YouTubeShortlistSaveResponseDto> SaveShortlistAsync(int userId, YouTubeShortlistSaveRequestDto request, CancellationToken ct = default)
        {
            var creatorIds = (request.CreatorIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .Take(20)
                .ToList();

            var channelIds = (request.ChannelIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            if (channelIds.Count > 0)
            {
                var mappedCreatorIds = await _db.Creators
                    .AsNoTracking()
                    .Where(c => channelIds.Contains(c.ChannelId))
                    .Select(c => c.CreatorId)
                    .ToListAsync(ct);

                creatorIds = creatorIds.Concat(mappedCreatorIds).Distinct().Take(20).ToList();
            }

            if (creatorIds.Count == 0 && channelIds.Count == 0)
            {
                return new YouTubeShortlistSaveResponseDto
                {
                    Saved = false,
                    SavedCount = 0,
                    Message = "Select at least one creator to save shortlist.",
                    CreatorIds = new List<int>(),
                    ChannelIds = new List<string>()
                };
            }

            var existingCreatorIds = await _db.Creators
                .AsNoTracking()
                .Where(c => creatorIds.Contains(c.CreatorId))
                .Select(c => c.CreatorId)
                .ToListAsync(ct);

            var workspaceMembership = await _db.WorkspaceMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.UserId == userId, ct);

            var loggedToWorkspace = false;
            if (workspaceMembership != null)
            {
                var metadata = new
                {
                    title = string.IsNullOrWhiteSpace(request.Title) ? "YouTube Intelligence Shortlist" : request.Title.Trim(),
                    creatorIds = existingCreatorIds,
                    channelIds,
                    searchQuery = request.SearchQuery,
                    campaignId = request.CampaignId,
                    notes = request.Notes,
                    source = "youtube-search-intelligence"
                };

                _db.WorkspaceAuditLogs.Add(new Domain.Entities.WorkspaceAuditLog
                {
                    WorkspaceId = workspaceMembership.WorkspaceId,
                    ActorUserId = userId,
                    Action = "youtube.shortlist.saved",
                    Target = request.CampaignId.HasValue ? $"campaign:{request.CampaignId.Value}" : "search-shortlist",
                    MetadataJson = JsonSerializer.Serialize(metadata),
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(ct);
                loggedToWorkspace = true;
            }

            var creatorIntelligenceRoute = request.CampaignId.HasValue
                ? $"/brand/creator-intelligence?campaignId={request.CampaignId.Value}&creatorIds={string.Join(",", existingCreatorIds)}"
                : $"/brand/creator-intelligence?creatorIds={string.Join(",", existingCreatorIds)}";

            return new YouTubeShortlistSaveResponseDto
            {
                Saved = true,
                SavedCount = Math.Max(existingCreatorIds.Count, channelIds.Count),
                LoggedToWorkspace = loggedToWorkspace,
                Message = loggedToWorkspace
                    ? "Shortlist saved and logged to workspace activity."
                    : "Shortlist prepared. Join a workspace to sync it with team activity.",
                WorkspaceRoute = "/workspace/team",
                CampaignRoute = request.CampaignId.HasValue ? $"/results/{request.CampaignId.Value}" : null,
                CreatorIntelligenceRoute = creatorIntelligenceRoute,
                CreatorIds = existingCreatorIds,
                ChannelIds = channelIds
            };
        }

        private async Task<List<YouTubeSearchCreatorDto>> SearchLiveYouTubeChannelsAsync(
            YouTubeSearchQueryRequestDto request,
            int limit,
            List<string> tokens,
            string loweredQuery,
            string intentLabel,
            CancellationToken ct)
        {
            if (!IsApiKeyConfigured()) return new List<YouTubeSearchCreatorDto>();
            if (!_quota.CanConsume(101)) return new List<YouTubeSearchCreatorDto>();

            var client = _http.CreateClient();
            var regionCode = NormalizeCountryCode(request.Country);
            var maxResults = Math.Min(limit, 50);
            var searchUrl = "https://www.googleapis.com/youtube/v3/search"
                + "?part=snippet&type=channel"
                + $"&q={Uri.EscapeDataString(request.Query)}"
                + $"&maxResults={maxResults}"
                + (string.IsNullOrWhiteSpace(regionCode) ? "" : $"&regionCode={regionCode}")
                + $"&key={_apiKey}";

            _quota.Consume(100);
            var searchResponse = await client.GetFromJsonAsync<YtSearchResponse>(searchUrl, ct);
            var channelIds = searchResponse?.Items
                ?.Select(i => i.Id?.ChannelId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (channelIds.Count == 0)
                return new List<YouTubeSearchCreatorDto>();

            var channelsUrl = "https://www.googleapis.com/youtube/v3/channels"
                + "?part=snippet,statistics"
                + $"&id={string.Join(",", channelIds)}"
                + $"&maxResults={channelIds.Count}"
                + $"&key={_apiKey}";

            _quota.Consume(1);
            var channelsResponse = await client.GetFromJsonAsync<YtChannelsResponse>(channelsUrl, ct);
            var liveChannels = channelsResponse?.Items ?? new List<YtChannelItem>();
            if (liveChannels.Count == 0) return new List<YouTubeSearchCreatorDto>();

            var dbCreators = await _db.Creators
                .AsNoTracking()
                .Where(c => channelIds.Contains(c.ChannelId))
                .Select(c => new
                {
                    c.CreatorId,
                    c.ChannelId,
                    c.Category,
                    c.Country,
                    c.Language,
                    c.EngagementRate,
                    c.ChannelUrl
                })
                .ToListAsync(ct);

            var dbByChannel = dbCreators
                .GroupBy(c => c.ChannelId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            float[]? queryEmbedding = null;
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                queryEmbedding = await _nlp.GetEmbeddingAsync(request.Query);
            }

            var output = new List<YouTubeSearchCreatorDto>();

            foreach (var item in liveChannels)
            {
                var channelId = item.Id ?? string.Empty;
                if (string.IsNullOrWhiteSpace(channelId)) continue;

                var local = dbByChannel.TryGetValue(channelId, out var mapped) ? mapped : null;
                var channelName = item.Snippet?.Title ?? "Unknown channel";
                var description = item.Snippet?.Description ?? string.Empty;
                var subscribers = ParseLong(item.Statistics?.SubscriberCount);
                var totalViews = ParseLong(item.Statistics?.ViewCount);
                var videoCount = (int)Math.Min(int.MaxValue, ParseLong(item.Statistics?.VideoCount));

                var category = !string.IsNullOrWhiteSpace(local?.Category)
                    ? local.Category
                    : InferCategoryFromText(channelName, description);

                var language = !string.IsNullOrWhiteSpace(local?.Language)
                    ? local.Language
                    : NormalizeLanguage(item.Snippet?.DefaultLanguage);

                var country = !string.IsNullOrWhiteSpace(local?.Country)
                    ? local.Country
                    : request.Country;

                var engagement = local != null
                    ? NormalizeEngagement(local.EngagementRate)
                    : EstimateFallbackEngagement(subscribers, totalViews, videoCount);

                var candidateText = string.Join(" ", new[] { channelName, category, description, language, country }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var keyword = ComputeKeywordScore(tokens, candidateText, loweredQuery);
                var engagementQuality = Math.Clamp(engagement * 12.0, 0.0, 1.0);
                var scaleQuality = Math.Clamp(Math.Log10(Math.Max(1000, subscribers)) / 7.0, 0.0, 1.0);

                var semantic = 0.0;
                if (queryEmbedding != null && !string.IsNullOrWhiteSpace(candidateText))
                {
                    var textForEmbedding = candidateText.Length > 400 ? candidateText[..400] : candidateText;
                    var candidateEmbedding = await _nlp.GetEmbeddingAsync(textForEmbedding);
                    if (candidateEmbedding != null)
                    {
                        semantic = Math.Max(0.0, _nlp.CosineSimilarity(queryEmbedding, candidateEmbedding));
                    }
                }

                var relevance = Math.Round((keyword * 0.46) + (semantic * 0.32) + (engagementQuality * 0.14) + (scaleQuality * 0.08), 4);

                output.Add(new YouTubeSearchCreatorDto
                {
                    CreatorId = local?.CreatorId ?? 0,
                    ChannelId = channelId,
                    ChannelName = channelName,
                    ThumbnailUrl = item.Snippet?.Thumbnails?.High?.Url
                        ?? item.Snippet?.Thumbnails?.Medium?.Url
                        ?? item.Snippet?.Thumbnails?.Default?.Url,
                    Category = category,
                    Country = country,
                    Language = language,
                    Subscribers = subscribers,
                    TotalViews = totalViews,
                    VideoCount = videoCount,
                    EngagementRate = engagement,
                    RelevanceScore = relevance,
                    RelevanceReason = BuildRelevanceReason(tokens, channelName, category, description, semantic, engagementQuality),
                    SuggestedActions = BuildSuggestedActions(intentLabel),
                    ChannelUrl = local?.ChannelUrl ?? $"https://youtube.com/channel/{channelId}"
                });
            }

            return output
                .OrderByDescending(x => x.RelevanceScore)
                .ThenByDescending(x => x.Subscribers)
                .Take(limit)
                .ToList();
        }

        private async Task<YouTubeCreatorAnalysisResponseDto?> AnalyzeByChannelIdAsync(string channelId, string? mode, string? searchContext, CancellationToken ct)
        {
            if (!IsApiKeyConfigured()) return null;

            var normalizedMode = NormalizeMode(mode);
            var take = normalizedMode switch
            {
                "last20" => 20,
                "channel" => 50,
                _ => 10
            };

            var liveVideos = await FetchLiveChannelVideosAsync(channelId, take, ct);
            if (liveVideos.Count == 0) return null;

            var avgViews = liveVideos.Average(v => v.ViewCount);
            var avgLikes = liveVideos.Average(v => v.LikeCount);
            var avgComments = liveVideos.Average(v => v.CommentCount);
            var avgEngagement = Math.Clamp(liveVideos.Average(v => NormalizeEngagement(v.EngagementRate)), 0.0, 1.0);
            var subscribers = await FetchLiveSubscribersAsync(channelId, ct);

            var topKeywords = ExtractTopKeywords(liveVideos, "", "").Take(8).ToList();
            var momentum = BuildMomentumScore(liveVideos, avgEngagement, subscribers);
            var fit = await BuildCampaignFitAsync(topKeywords, "", searchContext);
            var channelName = liveVideos.Select(v => v.ChannelTitle).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? channelId;

            var aiNarrative = await BuildAiNarrativeAsync(
                channelName,
                normalizedMode,
                liveVideos.Count,
                avgViews,
                avgEngagement,
                momentum,
                fit,
                topKeywords,
                searchContext);

            return new YouTubeCreatorAnalysisResponseDto
            {
                CreatorId = 0,
                ChannelName = channelName,
                Mode = normalizedMode,
                VideosAnalyzed = liveVideos.Count,
                FromDateUtc = liveVideos.Count > 0 ? liveVideos.Min(v => v.PublishedAt) : null,
                ToDateUtc = liveVideos.Count > 0 ? liveVideos.Max(v => v.PublishedAt) : null,
                AverageViews = Math.Round(avgViews, 2),
                AverageLikes = Math.Round(avgLikes, 2),
                AverageComments = Math.Round(avgComments, 2),
                AverageEngagementRate = Math.Round(avgEngagement, 4),
                MomentumScore = momentum,
                CampaignFitLabel = fit,
                DataQuality = "Live YouTube API sample",
                AiNarrative = aiNarrative,
                TopKeywords = topKeywords,
                TopVideos = liveVideos
                    .OrderByDescending(v => v.ViewCount)
                    .Take(5)
                    .Select(v => new YouTubeAnalysisVideoDto
                    {
                        VideoId = v.VideoId,
                        Title = v.Title,
                        ViewCount = v.ViewCount,
                        LikeCount = v.LikeCount,
                        CommentCount = v.CommentCount,
                        EngagementRate = NormalizeEngagement(v.EngagementRate),
                        PublishedAt = v.PublishedAt
                    })
                    .ToList(),
                ActionPlan = BuildActionPlan(fit, avgEngagement, momentum, topKeywords)
            };
        }

        private async Task<long> FetchLiveSubscribersAsync(string channelId, CancellationToken ct)
        {
            if (!IsApiKeyConfigured()) return 0;
            if (!_quota.CanConsume(1)) return 0;

            var client = _http.CreateClient();
            var url = "https://www.googleapis.com/youtube/v3/channels"
                + "?part=statistics"
                + $"&id={Uri.EscapeDataString(channelId)}"
                + $"&key={_apiKey}";

            _quota.Consume(1);
            var response = await client.GetFromJsonAsync<YtChannelsResponse>(url, ct);
            return ParseLong(response?.Items?.FirstOrDefault()?.Statistics?.SubscriberCount);
        }

        private async Task<List<LiveVideoRow>> FetchLiveChannelVideosAsync(string channelId, int take, CancellationToken ct)
        {
            if (!IsApiKeyConfigured()) return new List<LiveVideoRow>();
            if (!_quota.CanConsume(2)) return new List<LiveVideoRow>();

            var uploadsPlaylistId = channelId.StartsWith("UC", StringComparison.Ordinal)
                ? "UU" + channelId[2..]
                : channelId;

            var client = _http.CreateClient();
            var maxResults = Math.Min(take, 50);
            var playlistUrl = "https://www.googleapis.com/youtube/v3/playlistItems"
                + "?part=contentDetails"
                + $"&playlistId={Uri.EscapeDataString(uploadsPlaylistId)}"
                + $"&maxResults={maxResults}"
                + $"&key={_apiKey}";

            _quota.Consume(1);
            var playlistResponse = await client.GetFromJsonAsync<YtPlaylistItemsResponse>(playlistUrl, ct);
            var videoIds = playlistResponse?.Items
                ?.Select(x => x.ContentDetails?.VideoId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct()
                .ToList() ?? new List<string>();

            if (videoIds.Count == 0) return new List<LiveVideoRow>();

            var videosUrl = "https://www.googleapis.com/youtube/v3/videos"
                + "?part=snippet,statistics"
                + $"&id={string.Join(",", videoIds)}"
                + $"&key={_apiKey}";

            _quota.Consume(1);
            var videosResponse = await client.GetFromJsonAsync<YtVideosResponse>(videosUrl, ct);
            var rows = videosResponse?.Items?.Select(v => new LiveVideoRow
            {
                VideoId = v.Id ?? string.Empty,
                Title = v.Snippet?.Title ?? string.Empty,
                ChannelTitle = v.Snippet?.ChannelTitle,
                Description = v.Snippet?.Description,
                Tags = v.Snippet?.Tags,
                ViewCount = ParseLong(v.Statistics?.ViewCount),
                LikeCount = ParseLong(v.Statistics?.LikeCount),
                CommentCount = ParseLong(v.Statistics?.CommentCount),
                PublishedAt = v.Snippet?.PublishedAt ?? DateTime.UtcNow,
                EngagementRate = ComputeVideoEngagement(v.Statistics?.ViewCount, v.Statistics?.LikeCount, v.Statistics?.CommentCount)
            }).ToList() ?? new List<LiveVideoRow>();

            return rows.OrderByDescending(v => v.PublishedAt).Take(take).ToList();
        }

        private static double ComputeVideoEngagement(string? viewCountRaw, string? likeCountRaw, string? commentCountRaw)
        {
            var views = ParseLong(viewCountRaw);
            if (views <= 0) return 0;
            var likes = ParseLong(likeCountRaw);
            var comments = ParseLong(commentCountRaw);
            return Math.Round((likes + comments) / (double)views, 6);
        }

        private static string NormalizeCountryCode(string? country)
        {
            var value = (country ?? string.Empty).Trim().ToUpperInvariant();
            if (value.Length == 2) return value;
            return string.Empty;
        }

        private static string? NormalizeLanguage(string? languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return null;
            var code = languageCode.Trim().ToLowerInvariant();
            return code switch
            {
                "en" => "English",
                "hi" => "Hindi",
                "ta" => "Tamil",
                "te" => "Telugu",
                "bn" => "Bengali",
                _ => code
            };
        }

        private static string InferCategoryFromText(string title, string description)
        {
            var text = $"{title} {description}".ToLowerInvariant();
            if (text.Contains("tech") || text.Contains("gadget") || text.Contains("ai")) return "Tech";
            if (text.Contains("fitness") || text.Contains("workout") || text.Contains("gym")) return "Fitness";
            if (text.Contains("game") || text.Contains("gaming") || text.Contains("esports")) return "Gaming";
            if (text.Contains("beauty") || text.Contains("makeup") || text.Contains("skincare")) return "Beauty";
            if (text.Contains("finance") || text.Contains("invest") || text.Contains("stock")) return "Finance";
            if (text.Contains("travel") || text.Contains("trip") || text.Contains("vlog")) return "Travel";
            return "General";
        }

        private static double EstimateFallbackEngagement(long subscribers, long totalViews, int videoCount)
        {
            if (videoCount <= 0 || subscribers <= 0 || totalViews <= 0) return 0.022;
            var avgViews = totalViews / (double)Math.Max(1, videoCount);
            var viewToSubs = avgViews / Math.Max(1.0, subscribers);
            var estimate = 0.012 + Math.Clamp(viewToSubs * 0.06, 0.0, 0.05);
            return Math.Round(Math.Clamp(estimate, 0.01, 0.08), 4);
        }

        private static long ParseLong(string? raw)
        {
            if (long.TryParse(raw, out var value)) return value;
            return 0;
        }

        private bool IsApiKeyConfigured()
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return false;
            if (_apiKey.Contains("YOUR", StringComparison.OrdinalIgnoreCase)) return false;
            if (_apiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private async Task<ZeroShotResult> BuildIntentAsync(string queryText, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(queryText))
            {
                return new ZeroShotResult(false, "General discovery", new Dictionary<string, double>());
            }

            var labels = new[]
            {
                "Brand awareness campaign",
                "Performance or conversion campaign",
                "Product review and consideration",
                "Regional language creator scouting",
                "Trend and content inspiration"
            };

            var result = await _nlp.ClassifyZeroShotAsync(queryText, labels);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.TopLabel))
            {
                var fallback = queryText.Contains("review", StringComparison.OrdinalIgnoreCase)
                    ? "Product review and consideration"
                    : queryText.Contains("hindi", StringComparison.OrdinalIgnoreCase) || queryText.Contains("tamil", StringComparison.OrdinalIgnoreCase)
                        ? "Regional language creator scouting"
                        : "Brand awareness campaign";
                return new ZeroShotResult(false, fallback, new Dictionary<string, double>());
            }

            return result;
        }

        private static string NormalizeMode(string? mode)
        {
            var value = (mode ?? string.Empty).Trim().ToLowerInvariant();
            return value switch
            {
                "last20" or "20" => "last20",
                "channel" or "all" or "whole" => "channel",
                _ => "last10"
            };
        }

        private static double ComputeKeywordScore(List<string> tokens, string haystack, string fullQueryLower)
        {
            var text = (haystack ?? string.Empty).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(text)) return 0;
            if (string.IsNullOrWhiteSpace(fullQueryLower)) return 0.42;

            var phrase = text.Contains(fullQueryLower) ? 1.0 : 0.0;
            if (tokens.Count == 0) return phrase > 0 ? 1.0 : 0.35;

            var overlap = tokens.Count(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
            var tokenScore = (double)overlap / tokens.Count;

            return Math.Clamp((phrase * 0.55) + (tokenScore * 0.45), 0.0, 1.0);
        }

        private static string BuildRelevanceReason(
            List<string> tokens,
            string channelName,
            string category,
            string? description,
            double semantic,
            double engagementQuality)
        {
            var name = (channelName ?? string.Empty).ToLowerInvariant();
            if (tokens.Any(t => name.Contains(t)))
                return "Strong name match with your search terms";

            if (!string.IsNullOrWhiteSpace(category) && tokens.Any(t => category.Contains(t, StringComparison.OrdinalIgnoreCase)))
                return "Category alignment matched your query intent";

            if (semantic >= 0.70)
                return "High semantic match from AI embedding similarity";

            if (engagementQuality >= 0.65)
                return "Solid engagement quality improves campaign readiness";

            return !string.IsNullOrWhiteSpace(description)
                ? "Description keywords and channel metadata matched"
                : "General relevance from metadata and audience signals";
        }

        private static List<string> BuildSuggestedActions(string intent)
        {
            var actions = new List<string>
            {
                "Analyze last 10 videos",
                "Analyze last 20 videos",
                "Analyze whole channel"
            };

            if (intent.Contains("Performance", StringComparison.OrdinalIgnoreCase))
                actions.Add("Prioritize conversion-focused brief");
            else if (intent.Contains("awareness", StringComparison.OrdinalIgnoreCase))
                actions.Add("Prioritize awareness-style creative brief");
            else
                actions.Add("Compare fit against your campaign persona");

            return actions;
        }

        private static double NormalizeEngagement(double raw)
        {
            if (!double.IsFinite(raw) || raw <= 0) return 0;
            return raw > 1 ? Math.Clamp(raw / 100.0, 0.0, 1.0) : Math.Clamp(raw, 0.0, 1.0);
        }

        private static List<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string>();

            return Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+")
                .Where(t => t.Length >= 3)
                .Where(t => !StopWords.Contains(t))
                .Distinct()
                .ToList();
        }

        private static double EstimateAvgViews(long totalViews, int videoCount)
        {
            if (videoCount <= 0) return 0;
            return Math.Round((double)totalViews / videoCount, 2);
        }

        private static List<string> ExtractTopKeywords(List<Domain.Entities.Video> videos, Domain.Entities.Creator creator)
        {
            var text = string.Join(" ", videos.Select(v => $"{v.Title} {v.Description} {v.Tags}"));
            if (string.IsNullOrWhiteSpace(text))
                text = string.Join(" ", new[] { creator.Category, creator.Description, creator.ChannelTags, creator.Language });

            var tokens = Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+")
                .Where(t => t.Length >= 4)
                .Where(t => !StopWords.Contains(t));

            return tokens
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(g.Key))
                .Take(12)
                .ToList();
        }

        private static List<string> ExtractTopKeywords(List<LiveVideoRow> videos, string category, string? fallbackText)
        {
            var text = string.Join(" ", videos.Select(v => $"{v.Title} {v.Description} {string.Join(' ', v.Tags ?? new List<string>())}"));
            if (string.IsNullOrWhiteSpace(text))
                text = string.Join(" ", new[] { category, fallbackText });

            var tokens = Regex.Split(text.ToLowerInvariant(), "[^a-z0-9]+")
                .Where(t => t.Length >= 4)
                .Where(t => !StopWords.Contains(t));

            return tokens
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(g.Key))
                .Take(12)
                .ToList();
        }

        private static double BuildMomentumScore(List<Domain.Entities.Video> videos, double avgEngagement, long subscribers)
        {
            if (videos.Count < 4)
            {
                var baseScore = (avgEngagement * 100.0) + (Math.Log10(Math.Max(1000, subscribers)) * 4.5);
                return Math.Round(Math.Clamp(baseScore, 35, 84), 1);
            }

            var ordered = videos.OrderBy(v => v.PublishedAt).ToList();
            var midpoint = Math.Max(1, ordered.Count / 2);
            var olderAvg = ordered.Take(midpoint).Average(v => v.ViewCount);
            var newerAvg = ordered.Skip(midpoint).Average(v => v.ViewCount);
            var growth = olderAvg <= 0 ? 0 : (newerAvg - olderAvg) / olderAvg;

            var score = 58 + (growth * 32) + (avgEngagement * 120);
            return Math.Round(Math.Clamp(score, 20, 98), 1);
        }

        private static double BuildMomentumScore(List<LiveVideoRow> videos, double avgEngagement, long subscribers)
        {
            if (videos.Count < 4)
            {
                var baseScore = (avgEngagement * 100.0) + (Math.Log10(Math.Max(1000, subscribers)) * 4.5);
                return Math.Round(Math.Clamp(baseScore, 35, 84), 1);
            }

            var ordered = videos.OrderBy(v => v.PublishedAt).ToList();
            var midpoint = Math.Max(1, ordered.Count / 2);
            var olderAvg = ordered.Take(midpoint).Average(v => v.ViewCount);
            var newerAvg = ordered.Skip(midpoint).Average(v => v.ViewCount);
            var growth = olderAvg <= 0 ? 0 : (newerAvg - olderAvg) / olderAvg;

            var score = 58 + (growth * 32) + (avgEngagement * 120);
            return Math.Round(Math.Clamp(score, 20, 98), 1);
        }

        private async Task<string> BuildCampaignFitAsync(List<string> topKeywords, string category, string? searchContext)
        {
            var text = string.Join(", ", topKeywords.Take(8));
            if (!string.IsNullOrWhiteSpace(category))
            {
                text = $"{category}. {text}";
            }
            if (!string.IsNullOrWhiteSpace(searchContext))
            {
                text = $"{searchContext}. {text}";
            }

            var labels = new[]
            {
                "Awareness",
                "Consideration",
                "Conversion",
                "Retention",
                "Community building"
            };

            var result = await _nlp.ClassifyZeroShotAsync(text, labels);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.TopLabel))
                return "Awareness";

            return result.TopLabel;
        }

        private async Task<string?> BuildAiNarrativeAsync(
            string channelName,
            string mode,
            int videosAnalyzed,
            double avgViews,
            double avgEngagement,
            double momentum,
            string fit,
            List<string> keywords,
            string? searchContext)
        {
            var system = "You are an influencer marketing analyst. Return exactly 3 short bullet points about channel performance, audience behavior, and campaign recommendation.";
            var user =
                $"Channel: {channelName}. Analysis mode: {mode}. Videos analyzed: {videosAnalyzed}. "
                + $"Avg views: {Math.Round(avgViews)}. Avg engagement: {(avgEngagement * 100):F2}%. Momentum score: {momentum}. "
                + $"Campaign fit: {fit}. Top topics: {string.Join(", ", keywords.Take(6))}. Search context: {searchContext ?? "none"}.";

            return await _groq.GenerateTextAsync(system, user, 220);
        }

        private async Task<string?> BuildAiSearchBriefAsync(string query, string intent, List<YouTubeSearchCreatorDto> results)
        {
            if (results.Count == 0) return null;

            var top = results.Take(3)
                .Select(r => $"{r.ChannelName} ({r.Category}, {(r.EngagementRate * 100):F1}% ER)")
                .ToList();

            var system = "You are a media planner. Write one concise sentence that summarizes this YouTube creator search shortlist and suggests what to analyze first.";
            var user = $"Query: {query}. Intent: {intent}. Top matches: {string.Join("; ", top)}.";
            return await _groq.GenerateTextAsync(system, user, 80);
        }

        private static List<string> BuildActionPlan(string fit, double avgEngagement, double momentum, List<string> keywords)
        {
            var actions = new List<string>();

            if (fit.Contains("Conversion", StringComparison.OrdinalIgnoreCase))
                actions.Add("Run creator with an offer-driven CTA and track click-to-sale attribution.");
            else if (fit.Contains("Consideration", StringComparison.OrdinalIgnoreCase))
                actions.Add("Use comparison, demo, and FAQ style integrations to improve consideration.");
            else
                actions.Add("Use story-led awareness creative with broad reach targeting.");

            actions.Add(avgEngagement >= 0.04
                ? "Prioritize deeper integration formats because engagement quality is healthy."
                : "Use shorter, hook-first scripts to improve watch retention and interaction rate.");

            actions.Add(momentum >= 70
                ? "Momentum is positive; schedule activation in the next 2 to 4 weeks."
                : "Momentum is moderate; validate with a pilot before scaling budget.");

            if (keywords.Count > 0)
                actions.Add($"Creative should mirror audience themes: {string.Join(", ", keywords.Take(3))}.");

            return actions;
        }

        private record YtSearchResponse(List<YtSearchItem>? Items);
        private record YtSearchItem(YtSearchId? Id, YtSearchSnippet? Snippet);
        private record YtSearchId(string? ChannelId);
        private record YtSearchSnippet(
            string? Title,
            string? ChannelTitle,
            string? Description,
            string? DefaultLanguage,
            DateTime? PublishedAt,
            YtThumbnails? Thumbnails);
        private record YtThumbnails(YtThumbnail? Default, YtThumbnail? Medium, YtThumbnail? High);
        private record YtThumbnail(string? Url);

        private record YtChannelsResponse(List<YtChannelItem>? Items);
        private record YtChannelItem(string? Id, YtSearchSnippet? Snippet, YtChannelStatistics? Statistics);
        private record YtChannelStatistics(string? SubscriberCount, string? ViewCount, string? VideoCount);

        private record YtPlaylistItemsResponse(List<YtPlaylistItem>? Items);
        private record YtPlaylistItem(YtContentDetails? ContentDetails);
        private record YtContentDetails(string? VideoId);

        private record YtVideosResponse(List<YtVideoItem>? Items);
        private record YtVideoItem(string? Id, YtVideoSnippet? Snippet, YtVideoStatistics? Statistics);
        private record YtVideoSnippet(string? Title, string? Description, string? ChannelTitle, DateTime? PublishedAt, List<string>? Tags);
        private record YtVideoStatistics(string? ViewCount, string? LikeCount, string? CommentCount);

        private sealed class LiveVideoRow
        {
            public string VideoId { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string? ChannelTitle { get; set; }
            public string? Description { get; set; }
            public List<string>? Tags { get; set; }
            public long ViewCount { get; set; }
            public long LikeCount { get; set; }
            public long CommentCount { get; set; }
            public double EngagementRate { get; set; }
            public DateTime PublishedAt { get; set; }
        }
    }
}
