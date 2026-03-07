namespace InfluencerMatch.Application.Interfaces
{
    /// <summary>
    /// Tracks daily YouTube Data API v3 quota usage across all services.
    /// Resets automatically at UTC midnight to align with Google's quota window.
    /// Default daily safety limit: 9,000 units (90% of the 10,000-unit hard cap).
    /// </summary>
    public interface IYouTubeQuotaTracker
    {
        /// <summary>Safety limit at which new API calls are blocked (default 9,000).</summary>
        int DailyLimit { get; }

        /// <summary>Units consumed so far today (UTC).</summary>
        int UsedToday { get; }

        /// <summary>Units remaining before the safety limit is reached.</summary>
        int Remaining { get; }

        /// <summary>Returns true when <paramref name="units"/> can be consumed without breaching the daily limit.</summary>
        bool CanConsume(int units);

        /// <summary>Records <paramref name="units"/> of quota as consumed.</summary>
        void Consume(int units);
    }
}
