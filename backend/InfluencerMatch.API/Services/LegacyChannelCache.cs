using System.Collections.Concurrent;
using System.Collections.Generic;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;

namespace InfluencerMatch.API.Services
{
    /// <summary>
    /// In-memory singleton cache for legacy Influencer YouTube data.
    /// Populated exclusively by the SuperAdmin "Refresh Legacy Channels" job so that
    /// no YouTube API calls are made automatically on every marketplace page load.
    ///
    /// Data is held for the lifetime of the process (lost on restart — requires one
    /// SuperAdmin job run after each deployment to warm the cache).
    /// </summary>
    public sealed class LegacyChannelCache
    {
        private readonly ConcurrentDictionary<string, LiveChannelSnapshot> _snapshots = new();
        private readonly ConcurrentDictionary<string, List<ChannelVideoDto>> _videos   = new();

        // ── Snapshot (channel stats) ─────────────────────────────────────────

        public LiveChannelSnapshot? GetSnapshot(string youtubeUrl)
            => _snapshots.TryGetValue(NormaliseKey(youtubeUrl), out var snap) ? snap : null;

        public void SetSnapshot(string youtubeUrl, LiveChannelSnapshot snapshot)
            => _snapshots[NormaliseKey(youtubeUrl)] = snapshot;

        // ── Recent videos ────────────────────────────────────────────────────

        public List<ChannelVideoDto> GetVideos(string channelId)
            => _videos.TryGetValue(channelId, out var vids) ? vids : new List<ChannelVideoDto>();

        public void SetVideos(string channelId, List<ChannelVideoDto> videos)
            => _videos[channelId] = videos;

        // ── Diagnostics ──────────────────────────────────────────────────────

        /// <summary>Number of legacy influencer channels currently cached.</summary>
        public int Count => _snapshots.Count;

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string NormaliseKey(string url) => url.Trim().ToLowerInvariant();
    }
}
