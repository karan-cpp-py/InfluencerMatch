using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace InfluencerMatch.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenRepository _tokenRepository;
        private readonly IGtmService _gtmService;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        public AuthService(IUserRepository userRepository, ITokenRepository tokenRepository, IGtmService gtmService, IConfiguration configuration, IMapper mapper)
        {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _gtmService = gtmService;
            _configuration = configuration;
            _mapper = mapper;
        }

        public async Task<AuthTokenResponseDto> RegisterAsync(UserRegisterDto dto, string? ipAddress = null)
        {
            var existing = await _userRepository.GetByEmailAsync(dto.Email);
            if (existing != null)
                throw new ApplicationException("Email already in use");

            var role = ResolveRole(dto);
            var customerType = NormalizeCustomerType(dto.CustomerType);

            var user = _mapper.Map<User>(dto);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            user.Role = role;
            user.CustomerType = customerType;
            user.Country = string.IsNullOrWhiteSpace(dto.Country) ? "Unknown" : dto.Country.Trim();
            user.CompanyName = string.IsNullOrWhiteSpace(dto.CompanyName) ? null : dto.CompanyName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
            user.CreatedAt = DateTime.UtcNow;

            await _userRepository.AddAsync(user);

            if (!string.IsNullOrWhiteSpace(dto.ReferralCode))
            {
                await _gtmService.ApplyReferralCodeAsync(user.UserId, dto.ReferralCode);
            }

            return await GenerateAuthTokensAsync(user, ipAddress);
        }

        public async Task<AuthTokenResponseDto> LoginAsync(UserLoginDto dto, string? ipAddress = null)
        {
            var bootstrapAdminAuth = await TryBootstrapSuperAdminLoginAsync(dto, ipAddress);
            if (bootstrapAdminAuth != null)
            {
                return bootstrapAdminAuth;
            }

            var user = await _userRepository.GetByEmailAsync(dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new ApplicationException("Invalid credentials");
            return await GenerateAuthTokensAsync(user, ipAddress);
        }

        private async Task<AuthTokenResponseDto?> TryBootstrapSuperAdminLoginAsync(UserLoginDto dto, string? ipAddress)
        {
            var configuredEmail = _configuration["BootstrapSuperAdmin:Email"];
            var configuredPassword = _configuration["BootstrapSuperAdmin:Password"];
            var configuredName = _configuration["BootstrapSuperAdmin:Name"];

            var adminEmail = string.IsNullOrWhiteSpace(configuredEmail)
                ? "superadmin@influencermatch.local"
                : configuredEmail.Trim();

            var adminPassword = string.IsNullOrWhiteSpace(configuredPassword)
                ? "SuperAdmin@123"
                : configuredPassword;

            var adminName = string.IsNullOrWhiteSpace(configuredName)
                ? "Platform SuperAdmin"
                : configuredName.Trim();

            if (!string.Equals(dto.Email?.Trim(), adminEmail, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(dto.Password, adminPassword, StringComparison.Ordinal))
            {
                return null;
            }

            var user = await _userRepository.GetByEmailAsync(adminEmail);
            if (user == null)
            {
                user = new User
                {
                    Name = adminName,
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    Role = "SuperAdmin",
                    CustomerType = "Individual",
                    Country = "Unknown",
                    CreatedAt = DateTime.UtcNow
                };

                await _userRepository.AddAsync(user);
            }
            else
            {
                var needsUpdate = false;

                if (!string.Equals(user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                {
                    user.Role = "SuperAdmin";
                    needsUpdate = true;
                }

                if (!BCrypt.Net.BCrypt.Verify(adminPassword, user.PasswordHash))
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
                    needsUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(user.Name))
                {
                    user.Name = adminName;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    await _userRepository.UpdateAsync(user);
                }
            }

            return await GenerateAuthTokensAsync(user, ipAddress);
        }

        public async Task<AuthTokenResponseDto> RefreshTokenAsync(string refreshToken, string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ApplicationException("Refresh token is required.");
            }

            var tokenHash = ComputeSha256(refreshToken);
            var stored = await _tokenRepository.GetByTokenHashAsync(tokenHash);
            if (stored == null || stored.User == null)
            {
                throw new ApplicationException("Invalid refresh token.");
            }

            if (stored.RevokedAt != null)
            {
                throw new ApplicationException("Refresh token has been revoked.");
            }

            if (stored.ExpiresAt <= DateTime.UtcNow)
            {
                throw new ApplicationException("Refresh token has expired.");
            }

            stored.RevokedAt = DateTime.UtcNow;
            stored.RevokedByIp = ipAddress;

            return await GenerateAuthTokensAsync(stored.User, ipAddress, stored.TokenFamily, stored);
        }

        public async Task RevokeTokenAsync(string refreshToken, string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return;
            }

            var tokenHash = ComputeSha256(refreshToken);
            var stored = await _tokenRepository.GetByTokenHashAsync(tokenHash);
            if (stored == null)
            {
                return;
            }

            stored.RevokedAt = DateTime.UtcNow;
            stored.RevokedByIp = ipAddress;
            await _tokenRepository.SaveChangesAsync();
        }

        private async Task<AuthTokenResponseDto> GenerateAuthTokensAsync(User user, string? ipAddress, string? tokenFamily = null, RefreshToken? replacedToken = null)
        {
            var accessToken = GenerateJwtToken(user, out var expiresInSeconds);
            var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var refreshTokenHash = ComputeSha256(rawRefreshToken);

            var refreshToken = new RefreshToken
            {
                UserId = user.UserId,
                TokenHash = refreshTokenHash,
                TokenFamily = tokenFamily ?? Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedByIp = ipAddress
            };

            if (replacedToken != null)
            {
                replacedToken.ReplacedByTokenHash = refreshTokenHash;
            }

            await _tokenRepository.AddRefreshTokenAsync(refreshToken);
            await _tokenRepository.SaveChangesAsync();

            return new AuthTokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = rawRefreshToken,
                ExpiresInSeconds = expiresInSeconds
            };
        }

        private string GenerateJwtToken(User user, out int expiresInSeconds)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);
            var tokenHandler = new JwtSecurityTokenHandler();

            var isAdmin = string.Equals(user.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
            expiresInSeconds = isAdmin ? 15 * 60 : 60 * 60;

            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("customer_type", user.CustomerType),
                new Claim("token_use", "access")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddSeconds(expiresInSeconds),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static string ComputeSha256(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes);
        }

        private static string ResolveRole(UserRegisterDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.Role))
            {
                return dto.Role.Trim();
            }

            return NormalizeCustomerType(dto.CustomerType) switch
            {
                "Brand" => "Brand",
                "Agency" => "Agency",
                "CreatorManager" => "CreatorManager",
                _ => "Individual"
            };
        }

        private static string NormalizeCustomerType(string? customerType)
        {
            if (string.IsNullOrWhiteSpace(customerType))
            {
                return "Individual";
            }

            return customerType.Trim().ToLowerInvariant() switch
            {
                "brand" => "Brand",
                "advertisingagencies" => "Agency",
                "advertisingagency" => "Agency",
                "agency" => "Agency",
                "individual" => "Individual",
                "individualuser" => "Individual",
                "creatormanager" => "CreatorManager",
                "talentagency" => "CreatorManager",
                _ => "Individual"
            };
        }
    }
}