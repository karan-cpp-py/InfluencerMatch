namespace InfluencerMatch.Application.DTOs
{
    public class DashboardConfigDto
    {
        public string CustomerType { get; set; } = string.Empty;
        public string PlanName { get; set; } = "Free";
        public string AnalyticsAccessLevel { get; set; } = "Basic";
        public bool CreatorDiscovery { get; set; }
        public bool CampaignManagement { get; set; }
        public bool MultiBrandCampaignManagement { get; set; }
        public bool BasicAnalytics { get; set; }
        public bool AdvancedAnalytics { get; set; }
        public bool ManageCreatorProfiles { get; set; }
        public bool TrackBrandDeals { get; set; }
        public bool ExportCreatorData { get; set; }
        public bool ApiAccess { get; set; }
        public string[] EnabledModules { get; set; } = [];
    }
}
