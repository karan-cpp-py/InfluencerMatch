using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Records a detected brand promotion inside a YouTube video (ad, sponsor, partner mention).
    /// </summary>
    public class BrandMention
    {
        public int    BrandMentionId    { get; set; }

        /// <summary>YouTube video ID where the mention was found.</summary>
        public string VideoId           { get; set; } = string.Empty;

        /// <summary>Title of the video at the time of detection (for quick reference).</summary>
        public string VideoTitle        { get; set; } = string.Empty;

        /// <summary>FK → Creator who uploaded the video.</summary>
        public int    CreatorId         { get; set; }

        /// <summary>
        /// Normalised brand name extracted from the mention.
        /// For hashtag methods this is the hashtag text; for @-mentions it is the handle.
        /// </summary>
        public string BrandName         { get; set; } = string.Empty;

        /// <summary>
        /// How the brand was detected: "Hashtag" (#ad/#sponsored/#partner),
        /// "Mention" (@handle), or "Keyword" (text-pattern match).
        /// </summary>
        public string DetectionMethod   { get; set; } = string.Empty;

        /// <summary>0.0 – 1.0 confidence based on detection strength.</summary>
        public double ConfidenceScore   { get; set; }

        public DateTime DetectedAt      { get; set; }

        // Navigation
        public Creator Creator          { get; set; } = null!;
    }
}
