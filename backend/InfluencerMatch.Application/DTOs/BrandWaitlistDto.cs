using System.ComponentModel.DataAnnotations;

namespace InfluencerMatch.Application.DTOs
{
    public class BrandWaitlistRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
