using System;

namespace InfluencerMatch.Domain.Entities
{
    public class CreatorAnalytics
    {
        public int CreatorAnalyticsId { get; set; }
        public int CreatorId { get; set; }
        public double AvgViews { get; set; }
        public double AvgLikes { get; set; }
        public double AvgComments { get; set; }
        /// <summary>EngagementRate = (AvgLikes + AvgComments) / AvgViews</summary>
        public double EngagementRate { get; set; }
        public DateTime CalculatedAt { get; set; }

        public Creator Creator { get; set; } = null!;
    }
}
