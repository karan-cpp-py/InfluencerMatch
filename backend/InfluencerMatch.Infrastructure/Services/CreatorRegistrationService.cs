using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace InfluencerMatch.Infrastructure.Services
{
    public class CreatorRegistrationService : ICreatorRegistrationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public CreatorRegistrationService(ApplicationDbContext db, IConfiguration config)
        {
            _db     = db;
            _config = config;
        }

        public async Task<CreatorRegisterResponseDto> RegisterCreatorAsync(CreatorRegisterRequestDto dto)
        {
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
                throw new InvalidOperationException("Email already in use.");

            var user = new User
            {
                Name         = dto.Name,
                Email        = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role         = "Creator",
                CustomerType = "CreatorManager",
                CompanyName  = null,
                Country      = string.IsNullOrWhiteSpace(dto.Country) ? "Unknown" : dto.Country,
                PhoneNumber  = null,
                CreatedAt    = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();   // get UserId back

            var profile = new CreatorProfile
            {
                UserId          = user.UserId,
                Country         = dto.Country,
                Language        = dto.Language,
                Category        = dto.Category,
                InstagramHandle = dto.InstagramHandle,
                ContactEmail    = dto.ContactEmail ?? dto.Email,
                Bio             = dto.Bio,
                CreatedAt       = DateTime.UtcNow
            };
            _db.CreatorProfiles.Add(profile);
            await _db.SaveChangesAsync();

            return new CreatorRegisterResponseDto
            {
                Token            = GenerateJwt(user),
                CreatorProfileId = profile.CreatorProfileId,
                UserId           = user.UserId
            };
        }

        public async Task<CreatorProfileDto?> GetProfileAsync(int userId)
        {
            var p = await _db.CreatorProfiles
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.UserId == userId);
            return p == null ? null : ToDto(p);
        }

        public async Task<CreatorProfileDto> UpdateProfileAsync(int userId, UpdateCreatorProfileDto dto)
        {
            var p = await _db.CreatorProfiles.FirstOrDefaultAsync(x => x.UserId == userId)
                    ?? throw new InvalidOperationException("Creator profile not found.");

            if (dto.Country         != null) p.Country         = dto.Country;
            if (dto.Language        != null) p.Language        = dto.Language;
            if (dto.Category        != null) p.Category        = dto.Category;
            if (dto.InstagramHandle != null) p.InstagramHandle = dto.InstagramHandle;
            if (dto.ContactEmail    != null) p.ContactEmail    = dto.ContactEmail;
            if (dto.Bio             != null) p.Bio             = dto.Bio;

            await _db.SaveChangesAsync();

            await _db.Entry(p).Reference(x => x.User).LoadAsync();
            return ToDto(p);
        }

        public async Task<CreatorOnboardingStatusDto> GetOnboardingStatusAsync(int userId, CancellationToken ct = default)
        {
            var profile = await _db.CreatorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId, ct)
                ?? throw new InvalidOperationException("Creator profile not found.");

            var channel = await _db.CreatorChannels
                .FirstOrDefaultAsync(c => c.CreatorProfileId == profile.CreatorProfileId, ct);

            var recentVideos = new List<ChannelVideo>();
            if (channel != null)
            {
                recentVideos = await _db.ChannelVideos
                    .Where(v => v.ChannelId == channel.ChannelId)
                    .OrderByDescending(v => v.PublishedAt)
                    .Take(10)
                    .ToListAsync(ct);
            }

            var pendingCollaborations = await _db.CollaborationRequests
                .CountAsync(r => r.CreatorProfileId == profile.CreatorProfileId && r.Status == "Pending", ct);

            var creator = await _db.Creators
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            CreatorScore? score = null;
            if (creator != null)
            {
                score = await _db.CreatorScores
                    .FirstOrDefaultAsync(s => s.CreatorId == creator.CreatorId, ct);
            }

            var completeness = CalculateCompleteness(profile, channel);
            var steps = BuildSteps(profile, channel, recentVideos, score);

            return new CreatorOnboardingStatusDto
            {
                ProfileCompletenessPercent = completeness,
                ChannelLinked = channel != null,
                WeeklyAlertCount = BuildWeeklyInsights(recentVideos, pendingCollaborations).Count,
                Steps = steps,
                ScoreChangeExplanations = BuildScoreExplanations(score),
                WeeklyInsights = BuildWeeklyInsights(recentVideos, pendingCollaborations)
            };
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static CreatorProfileDto ToDto(CreatorProfile p) => new()
        {
            CreatorProfileId = p.CreatorProfileId,
            UserId           = p.UserId,
            Name             = p.User?.Name    ?? string.Empty,
            Email            = p.User?.Email   ?? string.Empty,
            Country          = p.Country,
            Language         = p.Language,
            Category         = p.Category,
            InstagramHandle  = p.InstagramHandle,
            ContactEmail     = p.ContactEmail,
            Bio              = p.Bio,
            CreatedAt        = p.CreatedAt
        };

        private string GenerateJwt(User user)
        {
            var cfg = _config.GetSection("JwtSettings");
            var key = Encoding.UTF8.GetBytes(cfg["Secret"]!);
            var handler = new JwtSecurityTokenHandler();
            var desc = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("customer_type", user.CustomerType)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            return handler.WriteToken(handler.CreateToken(desc));
        }

        private static int CalculateCompleteness(CreatorProfile profile, CreatorChannel? channel)
        {
            var checks = new[]
            {
                !string.IsNullOrWhiteSpace(profile.Country),
                !string.IsNullOrWhiteSpace(profile.Language),
                !string.IsNullOrWhiteSpace(profile.Category),
                !string.IsNullOrWhiteSpace(profile.Bio),
                !string.IsNullOrWhiteSpace(profile.InstagramHandle),
                !string.IsNullOrWhiteSpace(profile.ContactEmail),
                channel != null
            };

            var done = checks.Count(x => x);
            return (int)Math.Round((done / (double)checks.Length) * 100, MidpointRounding.AwayFromZero);
        }

        private static List<CreatorOnboardingStepDto> BuildSteps(
            CreatorProfile profile,
            CreatorChannel? channel,
            List<ChannelVideo> recentVideos,
            CreatorScore? score)
        {
            return new List<CreatorOnboardingStepDto>
            {
                new() { Key = "profile", Title = "Complete profile details", Completed = !string.IsNullOrWhiteSpace(profile.Bio) && !string.IsNullOrWhiteSpace(profile.Category) },
                new() { Key = "channel", Title = "Link your YouTube channel", Completed = channel != null },
                new() { Key = "videos", Title = "Fetch recent video analytics", Completed = recentVideos.Count >= 3 },
                new() { Key = "score", Title = "Generate creator intelligence score", Completed = score != null }
            };
        }

        private static List<string> BuildScoreExplanations(CreatorScore? score)
        {
            if (score == null)
            {
                return new List<string>
                {
                    "We need analytics data from your linked channel before score-change reasons can be generated."
                };
            }

            var notes = new List<string>();
            if (score.EngagementComponent >= 25)
                notes.Add("Engagement is a major positive driver in your score right now.");
            else
                notes.Add("Engagement is below top-creator benchmark; improving comments and likes can raise score.");

            if (score.ViewsComponent >= 18)
                notes.Add("Average views are performing strongly against your niche benchmark.");
            else
                notes.Add("Average views are currently limiting your score momentum.");

            if (score.GrowthComponent >= 12)
                notes.Add("Subscriber growth trend contributed strongly this week.");
            else
                notes.Add("Growth trend is currently flat; consistent publishing can help accelerate it.");

            return notes;
        }

        private static List<string> BuildWeeklyInsights(List<ChannelVideo> videos, int pendingCollaborations)
        {
            var insights = new List<string>();

            if (videos.Count > 0)
            {
                var avgViews = videos.Average(v => v.ViewCount);
                insights.Add($"Your last {videos.Count} videos averaged {Math.Round(avgViews):N0} views.");

                var topVideo = videos.OrderByDescending(v => v.ViewCount).First();
                insights.Add($"Top recent video: '{topVideo.Title}' with {topVideo.ViewCount:N0} views.");
            }
            else
            {
                insights.Add("No recent videos detected yet. Link a channel to start weekly insights.");
            }

            if (pendingCollaborations > 0)
            {
                insights.Add($"You have {pendingCollaborations} pending collaboration request(s).");
            }

            return insights;
        }
    }
}
