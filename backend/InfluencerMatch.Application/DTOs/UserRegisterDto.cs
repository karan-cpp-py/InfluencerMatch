namespace InfluencerMatch.Application.DTOs
{
    public class UserRegisterDto
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string CustomerType { get; set; } = "Individual";
        public string Country { get; set; } = "Unknown";
        public string? PhoneNumber { get; set; }
        public string? ReferralCode { get; set; }

        // Kept for backward compatibility with existing role-based endpoints.
        public string? Role { get; set; }
    }
}