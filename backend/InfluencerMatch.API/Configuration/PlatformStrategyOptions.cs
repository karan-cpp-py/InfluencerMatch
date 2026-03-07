namespace InfluencerMatch.API.Configuration
{
    public class PlatformStrategyOptions
    {
        public string PositioningLine { get; set; } = "AI Creator Intelligence Platform for growth and sponsorship readiness.";
        public bool CreatorIntelligenceEnabled { get; set; } = true;
        public bool CreatorGraphEnabled { get; set; } = true;
        public bool CreatorGraphPublicOptIn { get; set; } = true;
        public bool BrandActivationEnabled { get; set; } = false;
        public bool BrandPilotInviteOnly { get; set; } = true;
        public int BrandActivationCreatorThreshold { get; set; } = 1000;
    }
}
