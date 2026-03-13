using System;
using System.Collections.Generic;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// Profile for a creator who has voluntarily registered on the platform.
    /// Linked 1-to-1 with a <see cref="User"/> (Role = "Creator").
    /// A creator can attach one YouTube <see cref="CreatorChannel"/>.
    /// </summary>
    public class CreatorProfile
    {
        public int  CreatorProfileId { get; set; }
        public int  UserId           { get; set; }   // FK → User (Role = "Creator")
        public User User             { get; set; } = null!;

        public string? Country         { get; set; }   // ISO 3166-1 alpha-2, e.g. "IN"
        public string? Language        { get; set; }   // Primary content language, e.g. "Hindi"
        public string? Category        { get; set; }   // e.g. "Tech", "Gaming"
        public string? InstagramHandle { get; set; }
        public string? ContactEmail    { get; set; }
        public string? Bio             { get; set; }
        public string? AudienceDemographicsJson { get; set; }
        public DateTime? AudienceDemographicsFetchedAt { get; set; }
        public string? YouTubeAnalyticsRefreshToken { get; set; }
        public DateTime? YouTubeAnalyticsConnectedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation
        public ICollection<CreatorChannel>      Channels              { get; set; } = new List<CreatorChannel>();
        public ICollection<CollaborationRequest> CollaborationRequests { get; set; } = new List<CollaborationRequest>();
    }
}
