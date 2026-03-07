using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Time-series snapshot of a <see cref="ChannelVideo"/>'s performance metrics.
    /// A new row is inserted every 6 hours by <c>VideoMetricsUpdateWorker</c>.
    /// Historical rows are never deleted — they feed the AI training dataset.
    /// </summary>
    public class VideoMetrics
    {
        public int    MetricId       { get; set; }
        /// <summary>FK → ChannelVideo.YoutubeVideoId.</summary>
        public string YoutubeVideoId { get; set; } = string.Empty;
        public ChannelVideo Video    { get; set; } = null!;

        public long Views    { get; set; }
        public long Likes    { get; set; }
        public long Comments { get; set; }

        public DateTime RecordedAt { get; set; }
    }
}
