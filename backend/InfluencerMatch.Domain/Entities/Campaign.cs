using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InfluencerMatch.Domain.Entities
{
    public class Campaign
    {
        [Key]
        public int CampaignId { get; set; }

        // Brand user
        [ForeignKey("User")]
        public int BrandId { get; set; }
        public User User { get; set; }

        public decimal Budget { get; set; }
        public string Category { get; set; }
        public string TargetLocation { get; set; }

        public ICollection<MatchResult> MatchResults { get; set; } = new List<MatchResult>();
    }
}