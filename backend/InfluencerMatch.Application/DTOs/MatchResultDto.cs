namespace InfluencerMatch.Application.DTOs
{
    public class MatchResultDto
    {
        public int InfluencerId { get; set; }
        public string SourceType { get; set; } = "Influencer";
        public int SourceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Followers { get; set; }
        public double EngagementRate { get; set; }
        public decimal PricePerPost { get; set; }
        public double Score { get; set; }
        public string[] WhyRecommended { get; set; } = System.Array.Empty<string>();
        public double ResponseRate { get; set; }
        public double CompletionRate { get; set; }
        public int PreviousCampaignOutcomes { get; set; }
        public string TrustBand { get; set; } = "Developing";
        /// <summary>Cosine similarity score between campaign and creator embeddings (0–1). Null when HuggingFace API is unavailable.</summary>
        public double? SemanticSimilarity { get; set; }
        /// <summary>LLM-generated one-sentence explanation of this match. Null when Groq API is unavailable.</summary>
        public string? AiMatchExplanation { get; set; }
    }
}