using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;

        public DashboardService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardConfigDto> GetDashboardConfigAsync(int userId)
        {
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId)
                ?? throw new InvalidOperationException("User not found.");

            var now = DateTime.UtcNow;
            var sub = await _db.UserSubscriptions
                .Include(s => s.Plan)
                .Where(s => s.UserId == userId && s.Status == "Active" && s.EndDate > now)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            var planName = sub?.Plan?.PlanName ?? "Free";
            var analytics = sub?.Plan?.AnalyticsAccessLevel ?? "Basic";
            var isAdvanced = analytics.Equals("Advanced", StringComparison.OrdinalIgnoreCase)
                             || analytics.Equals("Custom", StringComparison.OrdinalIgnoreCase);
            var isEnterprise = planName.Equals("Enterprise", StringComparison.OrdinalIgnoreCase);
            var exportAllowed = sub?.Plan?.ExportAllowed ?? false;

            var type = user.CustomerType;
            var config = new DashboardConfigDto
            {
                CustomerType = type,
                PlanName = planName,
                AnalyticsAccessLevel = analytics,
                CreatorDiscovery = type is "Brand" or "Agency" or "Individual" or "CreatorManager",
                CampaignManagement = type == "Brand",
                MultiBrandCampaignManagement = type == "Agency",
                BasicAnalytics = true,
                AdvancedAnalytics = isAdvanced || type == "Agency",
                ManageCreatorProfiles = type == "CreatorManager",
                TrackBrandDeals = type == "CreatorManager",
                ExportCreatorData = exportAllowed,
                ApiAccess = isEnterprise
            };

            config.EnabledModules = BuildModules(config);
            return config;
        }

        private static string[] BuildModules(DashboardConfigDto c)
        {
            var modules = new List<string>();

            if (c.CreatorDiscovery) modules.Add("creator-discovery");
            if (c.CampaignManagement) modules.Add("campaign-management");
            if (c.MultiBrandCampaignManagement) modules.Add("multi-brand-campaign-management");
            if (c.BasicAnalytics) modules.Add("basic-analytics");
            if (c.AdvancedAnalytics) modules.Add("advanced-analytics");
            if (c.ManageCreatorProfiles) modules.Add("creator-profile-management");
            if (c.TrackBrandDeals) modules.Add("brand-deal-tracking");
            if (c.ExportCreatorData) modules.Add("creator-export");
            if (c.ApiAccess) modules.Add("api-access");

            return modules.ToArray();
        }
    }
}
