using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthTokenResponseDto> RegisterAsync(UserRegisterDto dto, string? ipAddress = null);
        Task<AuthTokenResponseDto> LoginAsync(UserLoginDto dto, string? ipAddress = null);
        Task<AuthTokenResponseDto> RefreshTokenAsync(string refreshToken, string? ipAddress = null);
        Task RevokeTokenAsync(string refreshToken, string? ipAddress = null);
    }
}