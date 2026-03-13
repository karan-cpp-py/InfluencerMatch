using System;
using System.Collections.Generic;

namespace InfluencerMatch.Application.DTOs
{
    public class AudienceDemographicsIngestRequestDto
    {
        public string? AccessToken { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class CreatorYouTubeAnalyticsConnectRequestDto
    {
        public string RedirectUri { get; set; } = string.Empty;
    }

    public class CreatorYouTubeAnalyticsConnectCodeDto
    {
        public string RedirectUri { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class CreatorYouTubeAnalyticsConnectUrlDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class AudienceDemographicsDto
    {
        public string Source { get; set; } = "YouTubeAnalytics";
        public DateTime FetchedAtUtc { get; set; }
        public DateTime WindowStartDate { get; set; }
        public DateTime WindowEndDate { get; set; }

        public List<AudienceBreakdownPointDto> CountryBreakdown { get; set; } = new();
        public List<AudienceBreakdownPointDto> AgeBreakdown { get; set; } = new();
        public List<AudienceBreakdownPointDto> GenderBreakdown { get; set; } = new();
    }

    public class AudienceBreakdownPointDto
    {
        public string Key { get; set; } = string.Empty;
        public double Percentage { get; set; }
    }
}
