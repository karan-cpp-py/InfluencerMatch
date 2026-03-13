namespace InfluencerMatch.Application.DTOs
{
    public class UserLoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class GoogleLoginDto
    {
        public string IdToken { get; set; } = string.Empty;
    }

    public class EmailVerificationRequestDto
    {
        public string Token { get; set; } = string.Empty;
    }

    public class PasswordResetRequestDto
    {
        public string Email { get; set; } = string.Empty;
    }

    public class PasswordResetConfirmDto
    {
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
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
        public bool EmailVerified { get; set; }
        public string? VerificationToken { get; set; }

        // Legacy compatibility for existing frontend clients.
        public string Token => AccessToken;
    }
}