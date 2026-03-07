using System;
using System.Collections.Generic;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// A YouTube channel linked by a registered <see cref="CreatorProfile"/>.
    /// This is distinct from the scraped <see cref="Creator"/> table —
    /// <see cref="CreatorChannel"/> represents verified, self-registered creators
    /// shown in the brand discovery marketplace.
    /// </summary>
    public class CreatorChannel
    {
        public int    Id               { get; set; }
        /// <summary>YouTube channel ID (UC…), unique across the table.</summary>
        public string ChannelId        { get; set; } = string.Empty;
        public int    CreatorProfileId { get; set; }
        public CreatorProfile CreatorProfile { get; set; } = null!;

        public string  ChannelName   { get; set; } = string.Empty;
        public string? Description   { get; set; }   // max 500 chars
        public string? ChannelUrl    { get; set; }   // URL originally submitted by creator
        public string? ThumbnailUrl  { get; set; }

        public long   Subscribers { get; set; }
        public long   TotalViews  { get; set; }
        public int    VideoCount  { get; set; }

        /// <summary>Computed engagement rate (updated by background worker).</summary>
        public double EngagementRate { get; set; }

        /// <summary>Nano | Micro | MidTier | Macro | Mega (re-computed on stats refresh).</summary>
        public string? CreatorTier { get; set; }

        /// <summary>When the YouTube channel was originally created.</summary>
        public DateTime? ChannelPublishedAt { get; set; }

        /// <summary>True once an admin or automated check has validated the channel.</summary>
        public bool IsVerified { get; set; }

        public DateTime? LastStatsUpdatedAt { get; set; }
        public DateTime  CreatedAt          { get; set; }

        // Navigation
        public ICollection<ChannelVideo> Videos { get; set; } = new List<ChannelVideo>();
    }
}
