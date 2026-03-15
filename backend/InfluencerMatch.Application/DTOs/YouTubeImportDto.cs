using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    /// <summary>Request body for POST /api/admin/jobs/youtube-import</summary>
    public class YouTubeImportRequestDto
    {
        /// <summary>Search query, e.g. "cooking recipes", "fitness hindi"</summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>ISO 3166-1 alpha-2 country code to filter by, e.g. "IN". Optional.</summary>
        public string? CountryCode { get; set; }

        /// <summary>Max channels to import per run (1–50). Default 20.</summary>
        public int MaxResults { get; set; } = 20;

        /// <summary>Category tag to assign to imported creators, e.g. "Cooking".</summary>
        public string? Category { get; set; }

        /// <summary>How many recent videos to fetch per channel for analytics (3–10). Default 10.</summary>
        public int MaxVideosPerChannel { get; set; } = 10;

        /// <summary>
        /// When true (default), importer upserts rows into Creators/Videos tables.
        /// Set false for non-persistent preview runs.
        /// </summary>
        public bool PersistResults { get; set; } = true;

        /// <summary>
        /// When true, generates AI-friendly summaries/signals for each row.
        /// </summary>
        public bool IncludeAiInsights { get; set; } = true;
    }

    /// <summary>One imported / updated channel row in the response.</summary>
    public class YouTubeImportResultRow
    {
        public string  ChannelId       { get; set; } = string.Empty;
        public string  ChannelName     { get; set; } = string.Empty;
        public long    Subscribers     { get; set; }
        public long    TotalViews      { get; set; }
        public int     VideoCount      { get; set; }
        public string? Country         { get; set; }
        public string? Category        { get; set; }
        public string? Email           { get; set; }
        public string? InstagramHandle { get; set; }
        public string? TwitterHandle   { get; set; }
        public string? ThumbnailUrl    { get; set; }
        public double  EngagementRate  { get; set; }
        public double  AvgViews        { get; set; }
        public double  MlFitScore      { get; set; }
        public List<string> AiSignals  { get; set; } = new();
        public string? AiBrief         { get; set; }
        public string? Error           { get; set; }
        public string  Status          { get; set; } = string.Empty; // "new" | "updated" | "skipped"
    }

    /// <summary>Full response for an import run.</summary>
    public class YouTubeImportResultDto
    {
        public int      Imported   { get; set; }
        public int      Updated    { get; set; }
        public int      Skipped    { get; set; }
        public int      Previewed  { get; set; }
        public int      QuotaUsed  { get; set; }
        public DateTime Timestamp  { get; set; }
        public List<YouTubeImportResultRow> Rows { get; set; } = new();
    }
}
