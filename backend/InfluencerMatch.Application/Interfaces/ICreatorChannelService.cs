using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ICreatorChannelService
    {
        /// <summary>
        /// Links a YouTube channel to the creator's profile by URL.
        /// Extracts the channel ID, validates against YouTube API,
        /// checks for duplicates, and fetches initial video data.
        /// </summary>
        Task<CreatorChannelDto> LinkChannelAsync(int creatorProfileId, string channelUrl,
            CancellationToken ct = default);

        /// <summary>Gets the linked channel for a creator profile (null if none).</summary>
        Task<CreatorChannelDto?> GetChannelAsync(int creatorProfileId,
            CancellationToken ct = default);

        /// <summary>
        /// Refreshes subscriber/view/video stats for the given channel from YouTube API.
        /// Called by the background stats worker.
        /// </summary>
        Task RefreshChannelStatsAsync(string channelId, CancellationToken ct = default);

        /// <summary>Returns recent videos for a channel ordered by PublishedAt desc.</summary>
        Task<List<ChannelVideoDto>> GetRecentVideosAsync(string channelId, int count = 10,
            CancellationToken ct = default);

        /// <summary>
        /// Resolves any YouTube channel URL/handle to a live snapshot from the YouTube API
        /// (subscribers, views, thumbnail, channel ID). Returns null when the API key is
        /// not configured, quota is exhausted, or the URL cannot be resolved.
        /// </summary>
        Task<LiveChannelSnapshot?> FetchLiveChannelByUrlAsync(string channelUrl,
            CancellationToken ct = default);
    }

    /// <summary>Lightweight snapshot returned directly from the YouTube Channels API.</summary>
    public class LiveChannelSnapshot
    {
        public string  ChannelId    { get; init; } = string.Empty;
        public string  ChannelName  { get; init; } = string.Empty;
        public string? ThumbnailUrl { get; init; }
        public long    Subscribers  { get; init; }
        public long    TotalViews   { get; init; }
        public int     VideoCount   { get; init; }
        public string? Country      { get; init; }
        public string? Description  { get; init; }
        public string? CreatorTier  { get; init; }
    }
}
