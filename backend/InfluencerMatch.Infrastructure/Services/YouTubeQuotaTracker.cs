using System;
using InfluencerMatch.Application.Interfaces;

namespace InfluencerMatch.Infrastructure.Services
{
    /// <summary>
    /// Thread-safe singleton that tracks daily YouTube API quota usage.
    /// Automatically resets at UTC midnight (aligned with Google's quota window).
    /// Blocks consumption once usage reaches 9,000 out of 10,000 allowed units,
    /// leaving a 1,000-unit safety buffer.
    /// </summary>
    public sealed class YouTubeQuotaTracker : IYouTubeQuotaTracker
    {
        private readonly object _lock = new();
        private int      _usedToday;
        private DateTime _trackedDate;

        public YouTubeQuotaTracker()
        {
            _trackedDate = DateTime.UtcNow.Date;
            _usedToday   = 0;
        }

        /// <inheritdoc />
        public int DailyLimit => 9_000;  // 90% of Google's 10,000-unit hard cap

        /// <inheritdoc />
        public int UsedToday
        {
            get { EnsureCurrentDay(); return _usedToday; }
        }

        /// <inheritdoc />
        public int Remaining => Math.Max(0, DailyLimit - UsedToday);

        /// <inheritdoc />
        public bool CanConsume(int units)
        {
            EnsureCurrentDay();
            return _usedToday + units <= DailyLimit;
        }

        /// <inheritdoc />
        public void Consume(int units)
        {
            lock (_lock)
            {
                EnsureCurrentDay();
                _usedToday += units;
            }
        }

        /// <summary>
        /// Rolls the counter over to the new UTC day when the calendar date changes.
        /// Uses double-checked locking so the lock is only acquired on the day boundary.
        /// </summary>
        private void EnsureCurrentDay()
        {
            var today = DateTime.UtcNow.Date;
            if (_trackedDate == today) return;  // fast path — no lock needed

            lock (_lock)
            {
                if (_trackedDate != today)
                {
                    _trackedDate = today;
                    _usedToday   = 0;
                }
            }
        }
    }
}
