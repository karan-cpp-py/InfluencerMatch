namespace InfluencerMatch.Application.DTOs
{
    public class InfluencerDto
    {
        public int InfluencerId { get; set; }
        public int UserId { get; set; }
        public string InstagramLink { get; set; }
        public string YouTubeLink { get; set; }
        public int Followers { get; set; }
        public double EngagementRate { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public decimal PricePerPost { get; set; }
    }
}