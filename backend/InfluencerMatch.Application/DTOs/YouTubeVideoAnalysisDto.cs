using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    public class YouTubeVideoAnalysisRequestDto
    {
        public string? CreatorName { get; set; }
        public string? ChannelId { get; set; }
        public YouTubeVideoDto? Video { get; set; }
        public YouTubeChannelContextDto? ChannelContext { get; set; }
        public List<YouTubeCommentDto> Comments { get; set; } = new();
        public bool AutoFetchComments { get; set; } = true;
        public int MaxCommentsToFetch { get; set; } = 500;
        public List<YouTubeTimeSeriesPointDto> TimeSeries { get; set; } = new();
        public DateTime? TodayUtc { get; set; }
    }

    public class YouTubeVideoDto
    {
        public string? VideoId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
        public string? CategoryId { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? Duration { get; set; }
        public bool? MadeForKids { get; set; }
        public string? DefaultLanguage { get; set; }
        public string? DefaultAudioLanguage { get; set; }
        public YouTubeVideoStatisticsDto? Statistics { get; set; }
    }

    public class YouTubeVideoStatisticsDto
    {
        public long? ViewCount { get; set; }
        public long? LikeCount { get; set; }
        public long? CommentCount { get; set; }
        public long? FavoriteCount { get; set; }
    }

    public class YouTubeChannelContextDto
    {
        public string? Title { get; set; }
        public string? Country { get; set; }
        public long? SubscriberCount { get; set; }
    }

    public class YouTubeCommentDto
    {
        public string? AuthorDisplayName { get; set; }
        public long? LikeCount { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? TextOriginal { get; set; }
    }

    public class YouTubeTimeSeriesPointDto
    {
        public DateTime TimestampUtc { get; set; }
        public long? ViewCount { get; set; }
        public long? LikeCount { get; set; }
        public long? CommentCount { get; set; }
    }
}
