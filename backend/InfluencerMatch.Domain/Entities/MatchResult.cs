using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InfluencerMatch.Domain.Entities
{
    public class MatchResult
    {
        [Key]
        public int MatchId { get; set; }

        [ForeignKey("Campaign")]
        public int CampaignId { get; set; }
        public Campaign Campaign { get; set; }

        [ForeignKey("Influencer")]
        public int InfluencerId { get; set; }
        public Influencer Influencer { get; set; }

        public double Score { get; set; }
    }
}