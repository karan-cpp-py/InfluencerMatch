using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthTokenResponseDto> RegisterAsync(UserRegisterDto dto, string? ipAddress = null);
        Task<AuthTokenResponseDto> LoginAsync(UserLoginDto dto, string? ipAddress = null);
        Task<AuthTokenResponseDto> LoginWithGoogleAsync(string idToken, string? ipAddress = null);
        Task<bool> VerifyEmailAsync(string token);
        Task<string?> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<AuthTokenResponseDto> RefreshTokenAsync(string refreshToken, string? ipAddress = null);
        Task RevokeTokenAsync(string refreshToken, string? ipAddress = null);
    }
}