using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;

namespace InfluencerMatch.Application.Services
{
    public class MatchService : IMatchService
    {
        private readonly ICampaignRepository _campaignRepo;
        private readonly IInfluencerRepository _influencerRepo;
        private readonly ICreatorRepository _creatorRepo;
        private readonly IMapper _mapper;
        private readonly IHuggingFaceNlpService _nlpService;
        private readonly IGroqLlmService _groqService;

        public MatchService(
            ICampaignRepository campaignRepo,
            IInfluencerRepository influencerRepo,
            ICreatorRepository creatorRepo,
            IMapper mapper,
            IHuggingFaceNlpService nlpService,
            IGroqLlmService groqService)
        {
            _campaignRepo  = campaignRepo;
            _influencerRepo = influencerRepo;
            _creatorRepo   = creatorRepo;
            _mapper        = mapper;
            _nlpService    = nlpService;
            _groqService   = groqService;
        }

        public async Task<IEnumerable<MatchResultDto>> MatchCampaignAsync(int campaignId, bool includeOverBudget = false)
        {
            var campaign = await _campaignRepo.GetByIdAsync(campaignId);
            if (campaign == null) return Enumerable.Empty<MatchResultDto>();

            var campaignCategory = (campaign.Category ?? string.Empty).Trim();
            var campaignLocation = (campaign.TargetLocation ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(campaignCategory) && string.IsNullOrWhiteSpace(campaignLocation))
            {
                // No targeting signal => avoid returning low-quality random matches.
                return Enumerable.Empty<MatchResultDto>();
            }

            var influencers = await _influencerRepo.GetAllWithUsersAsync();
            var creators = await _creatorRepo.GetAllWithUsersAsync();
            var creatorAnalytics = await _creatorRepo.GetLatestAnalyticsMapAsync(creators.Select(c => c.CreatorId));

            var candidates = new List<(MatchResultDto dto, string profileText, double catScore, double locScore)>();

            foreach (var inf in influencers)
            {
                var isOverBudget = inf.PricePerPost > campaign.Budget;
                if (!includeOverBudget && isOverBudget) continue;

                var categoryMatch = Similarity(campaignCategory, inf.Category);
                var locationMatch = Similarity(campaignLocation, inf.Location);
                var engagementPct = NormalizeEngagementPercent(inf.EngagementRate);
                var engagementScore = Math.Min(engagementPct / 8.0, 1.0);
                var followerScore = FollowerScore(inf.Followers);
                var budgetFit = BudgetFitScore(campaign.Budget, inf.PricePerPost);

                var relevanceSignal = Math.Max(categoryMatch, locationMatch);
                if (relevanceSignal < 0.35 && !(categoryMatch >= 0.25 && engagementScore >= 0.45))
                {
                    continue;
                }

                var score = (0.34 * categoryMatch
                            + 0.22 * locationMatch
                            + 0.18 * engagementScore
                            + 0.16 * budgetFit
                            + 0.10 * followerScore) * 100.0;

                var responseRate = Math.Clamp(45 + (engagementScore * 35) + (categoryMatch * 12) + (locationMatch * 8), 30, 98);
                var completionRate = Math.Clamp(48 + (engagementScore * 30) + (locationMatch * 12), 30, 99);
                var previousCampaignOutcomes = Math.Max(0, (int)Math.Round((inf.Followers / 120000.0) + (engagementScore * 5)));
                var trustBand = completionRate >= 85 && responseRate >= 80
                    ? "High Confidence"
                    : completionRate >= 70
                        ? "Stable"
                        : "Developing";

                var explain = new List<string>
                {
                    $"Budget fit score: {(budgetFit * 100):F0}%",
                    $"Engagement signal: {engagementPct:F2}%",
                    $"Trust indicators: {responseRate:F0}% response, {completionRate:F0}% completion"
                };

                if (categoryMatch >= 0.35)
                {
                    explain.Add($"Category alignment confidence: {(categoryMatch * 100):F0}%");
                }

                if (locationMatch >= 0.35)
                {
                    explain.Add($"Location fit confidence: {(locationMatch * 100):F0}%");
                }

                if (isOverBudget)
                {
                    explain.Add("Above budget: included because all-candidates mode is enabled");
                }

                var dto = new MatchResultDto
                {
                    InfluencerId = inf.InfluencerId,
                    SourceType = "Influencer",
                    SourceId = inf.InfluencerId,
                    Name = inf.User?.Name ?? "Unknown",
                    Followers = inf.Followers,
                    EngagementRate = engagementPct,
                    PricePerPost = inf.PricePerPost,
                    Score = Math.Round(score, 2),
                    WhyRecommended = explain.ToArray(),
                    ResponseRate = responseRate,
                    CompletionRate = completionRate,
                    PreviousCampaignOutcomes = previousCampaignOutcomes,
                    TrustBand = trustBand
                };

                var profileText = $"{dto.Name} influencer {inf.Category} {inf.Location} engagement {engagementPct:F2}% followers {inf.Followers}";
                candidates.Add((dto, profileText, categoryMatch, locationMatch));
            }

            foreach (var creator in creators)
            {
                var engagementPct = creatorAnalytics.TryGetValue(creator.CreatorId, out var analytics)
                    ? NormalizeEngagementPercent(analytics.EngagementRate)
                    : 0;

                var estimatedPrice = EstimateCreatorPricePerPost(creator.Subscribers, engagementPct);
                var isOverBudget = estimatedPrice > campaign.Budget;
                if (!includeOverBudget && isOverBudget) continue;

                var categoryMatch = Similarity(campaignCategory, creator.Category);
                var locationCandidate = string.Join(' ', new[] { creator.Country ?? string.Empty, creator.Region ?? string.Empty });
                var locationMatch = Similarity(campaignLocation, locationCandidate);
                var engagementScore = Math.Min(engagementPct / 8.0, 1.0);
                var followerScore = FollowerScore(creator.Subscribers);
                var budgetFit = BudgetFitScore(campaign.Budget, estimatedPrice);

                var relevanceSignal = Math.Max(categoryMatch, locationMatch);
                if (relevanceSignal < 0.35 && !(categoryMatch >= 0.25 && engagementScore >= 0.45))
                {
                    continue;
                }

                var score = (0.34 * categoryMatch
                            + 0.22 * locationMatch
                            + 0.18 * engagementScore
                            + 0.16 * budgetFit
                            + 0.10 * followerScore) * 100.0;

                var responseRate = Math.Clamp(45 + (engagementScore * 35) + (categoryMatch * 12) + (locationMatch * 8), 30, 98);
                var completionRate = Math.Clamp(48 + (engagementScore * 30) + (locationMatch * 12), 30, 99);
                var previousCampaignOutcomes = Math.Max(0, (int)Math.Round((creator.Subscribers / 120000.0) + (engagementScore * 5)));
                var trustBand = completionRate >= 85 && responseRate >= 80
                    ? "High Confidence"
                    : completionRate >= 70
                        ? "Stable"
                        : "Developing";

                var explain = new List<string>
                {
                    $"Budget fit score: {(budgetFit * 100):F0}%",
                    $"Engagement signal: {engagementPct:F2}%",
                    "Source: Registered creator profile"
                };

                if (categoryMatch >= 0.35)
                {
                    explain.Add($"Category alignment confidence: {(categoryMatch * 100):F0}%");
                }

                if (locationMatch >= 0.35)
                {
                    explain.Add($"Location fit confidence: {(locationMatch * 100):F0}%");
                }

                if (isOverBudget)
                {
                    explain.Add("Above budget: included because all-candidates mode is enabled");
                }

                var dto = new MatchResultDto
                {
                    InfluencerId = creator.CreatorId,
                    SourceType = "Creator",
                    SourceId = creator.CreatorId,
                    Name = creator.User?.Name ?? creator.ChannelName,
                    Followers = (int)Math.Min(int.MaxValue, creator.Subscribers),
                    EngagementRate = engagementPct,
                    PricePerPost = estimatedPrice,
                    Score = Math.Round(score, 2),
                    WhyRecommended = explain.ToArray(),
                    ResponseRate = responseRate,
                    CompletionRate = completionRate,
                    PreviousCampaignOutcomes = previousCampaignOutcomes,
                    TrustBand = trustBand
                };

                var profileText = $"{dto.Name} creator {creator.Category} {creator.Country} {creator.Region} engagement {engagementPct:F2}% subscribers {creator.Subscribers}";
                candidates.Add((dto, profileText, categoryMatch, locationMatch));
            }

            if (candidates.Count == 0)
            {
                return Enumerable.Empty<MatchResultDto>();
            }

            // ── Semantic reranking (batch embeddings) for top-20 candidates ────────────────
            var heuristicTop = candidates.OrderByDescending(r => r.dto.Score).Take(40).ToList();
            var remaining    = candidates.OrderByDescending(r => r.dto.Score).Skip(40).ToList();

            try
            {
                var campaignText = $"Brand campaign category {campaignCategory} location {campaignLocation} budget {campaign.Budget}";
                var creatorTextsFull = heuristicTop
                    .Select(r => r.profileText)
                    .ToList();

                var allTexts = new List<string> { campaignText }.Concat(creatorTextsFull).ToList();
                var embeddings = await _nlpService.GetEmbeddingsBatchAsync(allTexts);

                if (embeddings.Count == allTexts.Count)
                {
                    var campaignVector = embeddings[0];
                    for (int i = 0; i < heuristicTop.Count; i++)
                    {
                        var creatorVector = embeddings[i + 1];
                        var similarity    = _nlpService.CosineSimilarity(campaignVector, creatorVector);
                        var semanticBonus = Math.Max(0, similarity - 0.20) * 20;
                        heuristicTop[i].dto.Score += semanticBonus;
                        heuristicTop[i].dto.SemanticSimilarity = Math.Round(similarity, 3);

                        if (similarity > 0.45)
                        {
                            heuristicTop[i].dto.WhyRecommended = heuristicTop[i].dto.WhyRecommended
                                .Append($"Semantic content match: {similarity * 100:F0}%")
                                .ToArray();
                        }
                    }
                }
            }
            catch { /* embedding reranking is non-blocking — continue with heuristic order */ }

            // Generate LLM match explanation for top 5 (non-blocking)
            try
            {
                var top5 = heuristicTop.OrderByDescending(r => r.dto.Score).Take(5).ToList();
                foreach (var match in top5)
                {
                    var explanation = await _groqService.ExplainCreatorBrandMatchAsync(
                        campaignCategory,
                        campaignLocation,
                        match.dto.SourceType,
                        "",
                        Math.Min(match.dto.Score, 100));
                    if (!string.IsNullOrWhiteSpace(explanation))
                        match.dto.AiMatchExplanation = explanation;
                }
            }
            catch { /* LLM explanation is non-blocking */ }

            var ordered = heuristicTop
                .Concat(remaining)
                .OrderByDescending(r => r.dto.Score)
                .Select(r =>
                {
                    var semantic = r.dto.SemanticSimilarity ?? 0;
                    var relevance = Math.Max(Math.Max(r.catScore, r.locScore), semantic);
                    return new { Candidate = r.dto, Relevance = relevance };
                })
                .Where(x => x.Relevance >= 0.30)
                .Select(x => x.Candidate)
                .ToList();

            return ordered;
        }

        private static decimal EstimateCreatorPricePerPost(long subscribers, double engagementRate)
        {
            var subsFactor = Math.Max(1m, subscribers / 1000m);
            var engagementFactor = 0.7m + ((decimal)Math.Clamp(engagementRate, 0, 15) / 10m);
            var estimate = subsFactor * 2.2m * engagementFactor;
            return Math.Round(Math.Max(75m, estimate), 2);
        }

        private static double Similarity(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return 0;

            var l = Tokenize(left);
            var r = Tokenize(right);
            if (l.Count == 0 || r.Count == 0) return 0;

            var inter = l.Intersect(r).Count();
            if (inter == 0)
            {
                var a = left.Trim();
                var b = right.Trim();
                if (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase))
                {
                    return 0.55;
                }
                return 0;
            }

            var union = l.Union(r).Count();
            return union > 0 ? inter / (double)union : 0;
        }

        private static HashSet<string> Tokenize(string input)
        {
            var cleaned = new string(input
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
                .ToArray());

            return cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => s.Length >= 2)
                .ToHashSet();
        }

        private static double BudgetFitScore(decimal campaignBudget, decimal candidatePrice)
        {
            if (campaignBudget <= 0) return 0.5;

            if (candidatePrice <= campaignBudget)
            {
                var ratio = (double)(candidatePrice / campaignBudget);
                return Math.Clamp(1.0 - (ratio * 0.35), 0.55, 1.0);
            }

            var overBy = (double)((candidatePrice - campaignBudget) / campaignBudget);
            return Math.Clamp(1.0 - overBy, 0.0, 1.0);
        }

        private static double FollowerScore(long followers)
        {
            if (followers <= 0) return 0;
            var log = Math.Log10(followers + 1);
            return Math.Clamp((log - 3.0) / 4.0, 0.0, 1.0);
        }

        private static double NormalizeEngagementPercent(double raw)
        {
            if (!double.IsFinite(raw) || raw <= 0) return 0;
            var pct = raw > 1 ? raw : raw * 100;
            return Math.Clamp(pct, 0, 25);
        }
    }
}