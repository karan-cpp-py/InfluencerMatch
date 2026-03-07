namespace InfluencerMatch.Application.DTOs
{
    public class UserLoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class RefreshTokenRequestDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RevokeTokenRequestDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthTokenResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }

        // Legacy compatibility for existing frontend clients.
        public string Token => AccessToken;
    }
}