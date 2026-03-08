using System;

namespace InfluencerMatch.Infrastructure.Services
{
    /// <summary>
    /// Provides a uniform engagement-rate fallback (ratio format, e.g. 0.035 = 3.5%).
    /// </summary>
    public static class EngagementRateEstimator
    {
        private const double DefaultRate = 0.02; // 2.0%
        private const double MinRate = 0.005;    // 0.5%
        private const double MaxRate = 0.20;     // 20%

        public static double EstimateOrStored(
            double? storedRate,
            long subscribers,
            long totalViews,
            int videoCount,
            double? averageViews = null)
        {
            if (storedRate.HasValue && storedRate.Value > 0)
            {
                return Clamp(storedRate.Value);
            }

            var avgViews = averageViews ?? (videoCount > 0 ? totalViews / (double)videoCount : 0d);
            return EstimateFromAverages(subscribers, avgViews);
        }

        public static double EstimateFromAverages(long subscribers, double averageViews)
        {
            if (averageViews <= 0 || subscribers <= 0)
            {
                return DefaultRate;
            }

            // Heuristic: convert average view-to-subscriber ratio into engagement ratio.
            // Example: 10% avg view ratio -> ~1.5% engagement.
            var viewToSubscriberRatio = averageViews / subscribers;
            var estimated = viewToSubscriberRatio * 0.15;

            return Clamp(estimated);
        }

        public static double Clamp(double rate)
        {
            if (!double.IsFinite(rate) || rate <= 0)
            {
                return DefaultRate;
            }

            return Math.Clamp(rate, MinRate, MaxRate);
        }
    }
}
