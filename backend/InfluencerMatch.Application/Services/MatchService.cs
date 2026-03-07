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

        public MatchService(ICampaignRepository campaignRepo, IInfluencerRepository influencerRepo, ICreatorRepository creatorRepo, IMapper mapper)
        {
            _campaignRepo = campaignRepo;
            _influencerRepo = influencerRepo;
            _creatorRepo = creatorRepo;
            _mapper = mapper;
        }

        public async Task<IEnumerable<MatchResultDto>> MatchCampaignAsync(int campaignId, bool includeOverBudget = false)
        {
            var campaign = await _campaignRepo.GetByIdAsync(campaignId);
            if (campaign == null) return Enumerable.Empty<MatchResultDto>();

            var influencers = await _influencerRepo.GetAllWithUsersAsync();
            var creators = await _creatorRepo.GetAllWithUsersAsync();
            var creatorAnalytics = await _creatorRepo.GetLatestAnalyticsMapAsync(creators.Select(c => c.CreatorId));

            var results = new List<MatchResultDto>();

            foreach (var inf in influencers)
            {
                var isOverBudget = inf.PricePerPost > campaign.Budget;
                if (!includeOverBudget && isOverBudget) continue;

                double categoryMatch = string.Equals(inf.Category, campaign.Category, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                double locationMatch = string.Equals(inf.Location, campaign.TargetLocation, StringComparison.OrdinalIgnoreCase) ? 1 : 0;

                double score = (inf.EngagementRate * 5)
                               + ((double)inf.Followers / 1000)
                               - ((double)inf.PricePerPost / 100)
                               + (categoryMatch * 50)
                               + (locationMatch * 30);

                var responseRate = Math.Clamp(55 + (inf.EngagementRate * 8) + (categoryMatch * 12) - ((double)inf.PricePerPost / 150), 40, 98);
                var completionRate = Math.Clamp(60 + (inf.EngagementRate * 6) + (locationMatch * 8), 45, 99);
                var previousCampaignOutcomes = Math.Max(1, (int)Math.Round((inf.Followers / 100000.0) + (inf.EngagementRate * 2)));
                var trustBand = completionRate >= 85 && responseRate >= 80
                    ? "High Confidence"
                    : completionRate >= 70
                        ? "Stable"
                        : "Developing";

                var explain = new List<string>
                {
                    $"Budget fit: {Math.Round(((double)(campaign.Budget - inf.PricePerPost) / Math.Max(1, (double)campaign.Budget)) * 100, 1)}% headroom",
                    $"Engagement signal: {inf.EngagementRate:F2}%",
                    $"Trust indicators: {responseRate:F0}% response, {completionRate:F0}% completion"
                };

                if (categoryMatch > 0)
                {
                    explain.Add("Category alignment with campaign target");
                }

                if (locationMatch > 0)
                {
                    explain.Add("Location fit for campaign geography");
                }

                if (isOverBudget)
                {
                    explain.Add("Above budget: included because all-candidates mode is enabled");
                }

                results.Add(new MatchResultDto
                {
                    InfluencerId = inf.InfluencerId,
                    SourceType = "Influencer",
                    SourceId = inf.InfluencerId,
                    Name = inf.User?.Name,
                    Followers = inf.Followers,
                    EngagementRate = inf.EngagementRate,
                    PricePerPost = inf.PricePerPost,
                    Score = score,
                    WhyRecommended = explain.ToArray(),
                    ResponseRate = responseRate,
                    CompletionRate = completionRate,
                    PreviousCampaignOutcomes = previousCampaignOutcomes,
                    TrustBand = trustBand
                });
            }

            foreach (var creator in creators)
            {
                var engagementRate = creatorAnalytics.TryGetValue(creator.CreatorId, out var analytics)
                    ? analytics.EngagementRate * 100
                    : 0;

                var estimatedPrice = EstimateCreatorPricePerPost(creator.Subscribers, engagementRate);
                var isOverBudget = estimatedPrice > campaign.Budget;
                if (!includeOverBudget && isOverBudget) continue;

                double categoryMatch = string.Equals(creator.Category, campaign.Category, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                double locationMatch = IsCreatorLocationMatch(creator, campaign.TargetLocation) ? 1 : 0;

                double score = (engagementRate * 5)
                               + (creator.Subscribers / 1000.0)
                               - ((double)estimatedPrice / 100)
                               + (categoryMatch * 50)
                               + (locationMatch * 30);

                var responseRate = Math.Clamp(55 + (engagementRate * 0.8) + (categoryMatch * 12) - ((double)estimatedPrice / 150), 40, 98);
                var completionRate = Math.Clamp(60 + (engagementRate * 0.6) + (locationMatch * 8), 45, 99);
                var previousCampaignOutcomes = Math.Max(1, (int)Math.Round((creator.Subscribers / 100000.0) + (engagementRate * 0.2)));
                var trustBand = completionRate >= 85 && responseRate >= 80
                    ? "High Confidence"
                    : completionRate >= 70
                        ? "Stable"
                        : "Developing";

                var explain = new List<string>
                {
                    $"Budget fit: {Math.Round(((double)(campaign.Budget - estimatedPrice) / Math.Max(1, (double)campaign.Budget)) * 100, 1)}% headroom",
                    $"Engagement signal: {engagementRate:F2}%",
                    "Source: Registered creator profile"
                };

                if (categoryMatch > 0)
                {
                    explain.Add("Category alignment with campaign target");
                }

                if (locationMatch > 0)
                {
                    explain.Add("Location fit for campaign geography");
                }

                if (isOverBudget)
                {
                    explain.Add("Above budget: included because all-candidates mode is enabled");
                }

                results.Add(new MatchResultDto
                {
                    InfluencerId = creator.CreatorId,
                    SourceType = "Creator",
                    SourceId = creator.CreatorId,
                    Name = creator.User?.Name ?? creator.ChannelName,
                    Followers = (int)Math.Min(int.MaxValue, creator.Subscribers),
                    EngagementRate = engagementRate,
                    PricePerPost = estimatedPrice,
                    Score = score,
                    WhyRecommended = explain.ToArray(),
                    ResponseRate = responseRate,
                    CompletionRate = completionRate,
                    PreviousCampaignOutcomes = previousCampaignOutcomes,
                    TrustBand = trustBand
                });
            }

            return results.OrderByDescending(r => r.Score);
        }

        private static decimal EstimateCreatorPricePerPost(long subscribers, double engagementRate)
        {
            var subsFactor = Math.Max(1m, subscribers / 1000m);
            var engagementFactor = 0.7m + ((decimal)Math.Clamp(engagementRate, 0, 15) / 10m);
            var estimate = subsFactor * 2.2m * engagementFactor;
            return Math.Round(Math.Max(75m, estimate), 2);
        }

        private static bool IsCreatorLocationMatch(Creator creator, string targetLocation)
        {
            if (string.IsNullOrWhiteSpace(targetLocation)) return false;

            return string.Equals(creator.Country, targetLocation, StringComparison.OrdinalIgnoreCase)
                || string.Equals(creator.Region, targetLocation, StringComparison.OrdinalIgnoreCase);
        }
    }
}