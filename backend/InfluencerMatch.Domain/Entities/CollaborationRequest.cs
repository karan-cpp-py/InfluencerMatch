using System;

namespace InfluencerMatch.Domain.Entities
{
    /// <summary>
    /// A collaboration request sent by a brand (<see cref="User"/> with Role="Brand")
    /// to a registered <see cref="CreatorProfile"/>.
    /// </summary>
    public class CollaborationRequest
    {
        public int RequestId { get; set; }

        /// <summary>FK → User (Role = "Brand").</summary>
        public int  BrandUserId { get; set; }
        public User Brand       { get; set; } = null!;

        /// <summary>FK → CreatorProfile.</summary>
        public int           CreatorProfileId { get; set; }
        public CreatorProfile Creator         { get; set; } = null!;

        public string  CampaignTitle { get; set; } = string.Empty;
        public decimal Budget        { get; set; }
        public string? Message       { get; set; }

        /// <summary>Pending | Accepted | Rejected</summary>
        public string Status { get; set; } = "Pending";

        public DateTime  CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
