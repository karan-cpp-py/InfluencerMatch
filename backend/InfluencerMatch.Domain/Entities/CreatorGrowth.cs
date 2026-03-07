using System;

namespace InfluencerMatch.Domain.Entities
{
    public class CreatorGrowth
    {
        public int GrowthId { get; set; }
        public int CreatorId { get; set; }
        public long Subscribers { get; set; }
        public DateTime RecordedAt { get; set; }

        public Creator Creator { get; set; } = null!;
    }
}
