namespace InfluencerMatch.Application.DTOs
{
    public class CampaignDto
    {
        public int CampaignId { get; set; }
        public int BrandId { get; set; }
        public decimal Budget { get; set; }
        public string Category { get; set; }
        public string TargetLocation { get; set; }
    }
}