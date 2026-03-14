using System.Collections.Generic;
using System.Threading.Tasks;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IGroqLlmService
    {
        /// <summary>Low-level call to Groq llama-3.1-8b-instant. Returns null when key is missing or request fails.</summary>
        Task<string?> GenerateTextAsync(string systemPrompt, string userPrompt, int maxTokens = 400);

        /// <summary>Returns 4 AI-generated weekly coaching tips for a creator.</summary>
        Task<List<string>> GenerateCreatorCoachingTipsAsync(
            string channelName, string category,
            double avgViews, double engagementRate, string bestPostingWindow);

        /// <summary>One-sentence natural-language explanation of a creator–brand match.</summary>
        Task<string?> ExplainCreatorBrandMatchAsync(
            string campaignCategory, string campaignLocation,
            string creatorCategory, string creatorCountry, double matchScore);

        /// <summary>Two-sentence marketplace summary of a creator for brand browsing.</summary>
        Task<string?> SummarizeCreatorForBrandAsync(
            string channelName, string category,
            long subscribers, double engagementRate, string? topBrands);

        /// <summary>AI-generated summary of a YouTube video from its title and description.</summary>
        Task<string?> SummarizeVideoAsync(string title, string description);
    }
}
