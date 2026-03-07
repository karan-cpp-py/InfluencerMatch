using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InfluencerMatch.Domain.Entities
{
    public class Influencer
    {
        [Key]
        public int InfluencerId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }

        public string InstagramLink { get; set; }
        public string YouTubeLink { get; set; }
        public int Followers { get; set; }
        public double EngagementRate { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public decimal PricePerPost { get; set; }

        // navigation back to match results
        public ICollection<MatchResult> MatchResults { get; set; } = new List<MatchResult>();
    }
}