using System;
namespace InfluencerMatch.Domain.Entities
{
    public class Creator
    {
        public int    CreatorId   { get; set; }

        // ── Registered-user link ───────────────────────────────────────────
        /// <summary>FK to Users.UserId — every Creator must belong to a registered user.</summary>
        public int?   UserId      { get; set; }
        public User?  User        { get; set; }
        public string Platform   { get; set; } = "YouTube";
        public string ChannelId  { get; set; } = string.Empty; // unique
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>Channel description (first 500 chars stored for AI training context).</summary>
        public string? Description { get; set; }

        public long Subscribers { get; set; }
        public long TotalViews  { get; set; }
        public int  VideoCount  { get; set; }
        public string Category  { get; set; } = string.Empty;
        public string? Country  { get; set; }   // ISO 3166-1 alpha-2 e.g. "IN"

        // ── Creator segmentation ───────────────────────────────────────────
        /// <summary>Nano | Micro | MidTier | Macro | Mega</summary>
        public string? CreatorTier { get; set; }

        /// <summary>
        /// True when Subscribers &lt;= 500,000.
        /// Only IsSmallCreator=true creators are surfaced on the product UI;
        /// all creators (including larger ones) are retained for AI training.
        /// </summary>
        public bool IsSmallCreator { get; set; }

        // ── Language detection fields ──────────────────────────────────────
        /// <summary>Primary language detected from video titles, descriptions and comments.</summary>
        public string? Language { get; set; }          // e.g. "Hindi", "Tamil", "English"

        /// <summary>Region derived from country code + language (e.g. "India", "South India").</summary>
        public string? Region { get; set; }            // e.g. "India", "United States"

        /// <summary>Fraction of text samples that matched the dominant language (0–1).</summary>
        public double? LanguageConfidenceScore { get; set; }  // e.g. 0.6 = 60 % Hindi

        public DateTime  CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ── Static tier helpers ────────────────────────────────────────────
        /// <summary>
        /// Computes creator tier from subscriber count:<br/>
        /// Nano 1K–10K | Micro 10K–100K | MidTier 100K–500K | Macro 500K–5M | Mega 5M+
        /// </summary>
        public static string ComputeTier(long subscribers) => subscribers switch
        {
            >= 5_000_000 => "Mega",
            >= 500_000   => "Macro",
            >= 100_000   => "MidTier",
            >= 10_000    => "Micro",
            _            => "Nano"
        };

        /// <summary>Returns true when the creator has ≤ 500,000 subscribers (Nano/Micro/MidTier).</summary>
        public static bool ComputeIsSmall(long subscribers) => subscribers <= 500_000;
    }
}
