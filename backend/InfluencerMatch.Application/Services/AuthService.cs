using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Net.Http;
using System.Text.Json;
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
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly IMapper _mapper;

        public AuthService(IUserRepository userRepository, ITokenRepository tokenRepository, IGtmService gtmService, INotificationService notificationService, IConfiguration configuration, IMapper mapper)
        {
            _userRepository = userRepository;
            _tokenRepository = tokenRepository;
            _gtmService = gtmService;
            _notificationService = notificationService;
            _configuration = configuration;
            _mapper = mapper;
        }

        public async Task<AuthTokenResponseDto> RegisterAsync(UserRegisterDto dto, string? ipAddress = null)
        {
            if (!dto.AcceptTerms)
                throw new ApplicationException("You must accept Terms and Conditions to register.");

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
            user.EmailVerified = false;
            user.EmailVerificationToken = Guid.NewGuid().ToString("N");
            user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddHours(24);
            user.TermsAcceptedAt = DateTime.UtcNow;
            user.AuthProvider = "password";

            await _userRepository.AddAsync(user);

            if (!string.IsNullOrWhiteSpace(dto.ReferralCode))
            {
                await _gtmService.ApplyReferralCodeAsync(user.UserId, dto.ReferralCode);
            }

            if (EmailNotificationsEnabled())
            {
                var verifyLink = BuildFrontendLink("/verify-email", user.EmailVerificationToken, user.Email);
                await _notificationService.SendEmailAsync(
                    user.Email,
                    "Verify your InfluencerMatch email",
                    $"Welcome to InfluencerMatch. Verify your email by opening this link: {verifyLink}",
                    "auth.verify_email");
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

            if (!user.EmailVerified)
                throw new ApplicationException("Please verify your email before logging in.");

            return await GenerateAuthTokensAsync(user, ipAddress);
        }

        public async Task<AuthTokenResponseDto> LoginWithGoogleAsync(string idToken, string? ipAddress = null)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                throw new ApplicationException("Google ID token is required.");

            var tokenInfo = await ValidateGoogleTokenAsync(idToken);
            if (tokenInfo == null || string.IsNullOrWhiteSpace(tokenInfo.Email) || !tokenInfo.EmailVerified)
                throw new ApplicationException("Google login failed. Unable to verify Google account.");

            var user = await _userRepository.GetByEmailAsync(tokenInfo.Email);
            if (user == null)
            {
                user = new User
                {
                    Name = string.IsNullOrWhiteSpace(tokenInfo.Name) ? tokenInfo.Email.Split('@')[0] : tokenInfo.Name,
                    Email = tokenInfo.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
                    Role = "Individual",
                    CustomerType = "Individual",
                    Country = "Unknown",
                    CreatedAt = DateTime.UtcNow,
                    EmailVerified = true,
                    TermsAcceptedAt = DateTime.UtcNow,
                    AuthProvider = "google"
                };

                await _userRepository.AddAsync(user);
            }
            else
            {
                var needsUpdate = false;
                if (!user.EmailVerified)
                {
                    user.EmailVerified = true;
                    user.EmailVerificationToken = null;
                    user.EmailVerificationTokenExpiresAt = null;
                    needsUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(user.AuthProvider))
                {
                    user.AuthProvider = "google";
                    needsUpdate = true;
                }

                if (needsUpdate)
                    await _userRepository.UpdateAsync(user);
            }

            return await GenerateAuthTokensAsync(user, ipAddress);
        }

        public async Task<bool> VerifyEmailAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            var user = await _userRepository.GetByEmailVerificationTokenAsync(token);
            if (user == null) return false;
            if (!user.EmailVerificationTokenExpiresAt.HasValue || user.EmailVerificationTokenExpiresAt.Value < DateTime.UtcNow)
                return false;

            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiresAt = null;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<string?> RequestPasswordResetAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;

            var user = await _userRepository.GetByEmailAsync(email.Trim());
            if (user == null) return null;

            var token = Guid.NewGuid().ToString("N");
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(2);
            await _userRepository.UpdateAsync(user);

            if (EmailNotificationsEnabled())
            {
                var resetLink = BuildFrontendLink("/reset-password", token, user.Email);
                await _notificationService.SendEmailAsync(
                    user.Email,
                    "Reset your InfluencerMatch password",
                    $"Reset your InfluencerMatch password by opening this link: {resetLink}",
                    "auth.password_reset");
                return null;
            }

            return token;
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword)) return false;

            var user = await _userRepository.GetByPasswordResetTokenAsync(token);
            if (user == null) return false;
            if (!user.PasswordResetTokenExpiresAt.HasValue || user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
                return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            await _userRepository.UpdateAsync(user);
            return true;
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
                    CreatedAt = DateTime.UtcNow,
                    EmailVerified = true,
                    TermsAcceptedAt = DateTime.UtcNow,
                    AuthProvider = "password"
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
                ExpiresInSeconds = expiresInSeconds,
                EmailVerified = user.EmailVerified,
                VerificationToken = user.EmailVerified || EmailNotificationsEnabled() ? null : user.EmailVerificationToken
            };
        }

        private async Task<GoogleTokenInfo?> ValidateGoogleTokenAsync(string idToken)
        {
            using var client = new HttpClient();
            var response = await client.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}");
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;

            var aud = root.TryGetProperty("aud", out var audNode) ? audNode.GetString() : null;
            var expectedAud = _configuration["GoogleAuth:ClientId"];
            if (!string.IsNullOrWhiteSpace(expectedAud)
                && !string.Equals(expectedAud, aud, StringComparison.Ordinal))
            {
                return null;
            }

            return new GoogleTokenInfo
            {
                Email = root.TryGetProperty("email", out var emailNode) ? emailNode.GetString() ?? string.Empty : string.Empty,
                Name = root.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null,
                EmailVerified = root.TryGetProperty("email_verified", out var verifiedNode)
                    && string.Equals(verifiedNode.GetString(), "true", StringComparison.OrdinalIgnoreCase)
            };
        }

        private sealed class GoogleTokenInfo
        {
            public string Email { get; set; } = string.Empty;
            public string? Name { get; set; }
            public bool EmailVerified { get; set; }
        }

        private bool EmailNotificationsEnabled()
        {
            return bool.TryParse(_configuration["EmailNotifications:Enabled"], out var enabled) && enabled;
        }

        private string BuildFrontendLink(string path, string? token, string? email = null)
        {
            var baseUrl = _configuration["App:FrontendBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "http://localhost:5173";
            }

            var safeBase = baseUrl.TrimEnd('/');
            var queryParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(token))
            {
                queryParts.Add($"token={Uri.EscapeDataString(token)}");
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                queryParts.Add($"email={Uri.EscapeDataString(email)}");
            }

            var query = queryParts.Count > 0 ? $"?{string.Join("&", queryParts)}" : string.Empty;
            return $"{safeBase}{path}{query}";
        }

        private string GenerateJwtToken(User user, out int expiresInSeconds)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secret = jwtSettings["Secret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new ApplicationException("JWT secret is not configured.");
            }

            var key = Encoding.UTF8.GetBytes(secret);
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