using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InfluencerMatch.API.Services;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace InfluencerMatch.API.Controllers
{
    /// <summary>
    /// SuperAdmin-only control panel.
    /// All YouTube API jobs that used to run automatically are now triggered here.
    ///
    ///   POST /api/admin/seed                — bootstrap the first SuperAdmin account
    ///   GET  /api/admin/stats               — system overview (counts, quota)
    ///   POST /api/admin/jobs/analytics      — refresh creator analytics + growth snapshots
    ///   POST /api/admin/jobs/language       — run language detection for all creators
    ///   POST /api/admin/jobs/viral          — run viral content scoring
    ///   POST /api/admin/jobs/rising         — recalculate rising-creator growth scores
    ///   POST /api/admin/jobs/marketing      — run creator scoring + brand-promotion scan
    ///   POST /api/admin/jobs/creator-stats  — refresh registered CreatorChannel stats (Feature 7)
    ///   POST /api/admin/jobs/video-analytics — scan videos, detect brands, upsert VideoAnalytics (Feature 8)
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    public class SuperAdminController : ControllerBase
    {
        private readonly ApplicationDbContext       _db;
        private readonly ICreatorAnalyticsService   _analytics;
        private readonly ILanguageDetectionService  _language;
        private readonly IViralContentService       _viral;
        private readonly IRisingCreatorService      _rising;
        private readonly ICreatorScoringService     _scoring;
        private readonly IBrandPromotionService     _brand;
        private readonly ICreatorChannelService     _channel;
        private readonly IYouTubeQuotaTracker       _quota;
        private readonly LegacyChannelCache         _legacyCache;
        private readonly IHttpClientFactory         _httpClientFactory;
        private readonly string?                    _apiKey;

        private readonly IVideoAnalyticsService    _videoAnalytics;
        private readonly INotificationService      _notifications;
            private readonly IYouTubeCreatorImportService _youTubeImport;

        public SuperAdminController(
            ApplicationDbContext       db,
            ICreatorAnalyticsService   analytics,
            ILanguageDetectionService  language,
            IViralContentService       viral,
            IRisingCreatorService      rising,
            ICreatorScoringService     scoring,
            IBrandPromotionService     brand,
            ICreatorChannelService     channel,
            IYouTubeQuotaTracker       quota,
            LegacyChannelCache         legacyCache,
            IVideoAnalyticsService     videoAnalytics,
            INotificationService       notifications,
                IYouTubeCreatorImportService youTubeImport,
                IHttpClientFactory         httpClientFactory,
            IConfiguration             config)
        {
            _db                = db;
            _analytics         = analytics;
            _language          = language;
            _viral             = viral;
            _rising            = rising;
            _scoring           = scoring;
            _brand             = brand;
            _channel           = channel;
            _quota             = quota;
            _legacyCache       = legacyCache;
            _videoAnalytics    = videoAnalytics;
            _notifications     = notifications;
            _httpClientFactory = httpClientFactory;
                _youTubeImport     = youTubeImport;
            _apiKey =
                config["YouTube:ApiKey"]
                ?? config["YouTube__ApiKey"]
                ?? config["YOUTUBE_API_KEY"];
        }

        // ── Bootstrap ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creates the first SuperAdmin account.
        /// Returns 409 Conflict if a SuperAdmin already exists.
        /// This endpoint is intentionally open so the very first admin can be seeded.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("seed")]
        public async Task<IActionResult> Seed([FromBody] SeedAdminDto dto)
        {
            if (await _db.Users.AnyAsync(u => u.Role == "SuperAdmin"))
                return Conflict(new { error = "A SuperAdmin already exists. Login instead." });

            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Email and password are required." });

            var user = new User
            {
                Name         = dto.Name ?? "Super Admin",
                Email        = dto.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role         = "SuperAdmin",
                CustomerType = "Internal",
                Country      = "Unknown",
                CreatedAt    = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new { message = "SuperAdmin created. Please login now." });
        }

        // ── Stats overview ────────────────────────────────────────────────────

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            var creatorCount         = await _db.Creators.CountAsync();
            var creatorProfileCount  = await _db.CreatorProfiles.CountAsync();
            var channelCount         = await _db.CreatorChannels.CountAsync();
            var userCount            = await _db.Users.CountAsync();
            var collaborationCount   = await _db.CollaborationRequests.CountAsync();
            var waitlistPendingCount = await _db.BrandWaitlistEntries.CountAsync(x => x.Status == "Pending");
            var enterpriseLeadsCount = await _db.EnterpriseLeads.CountAsync();
            var recoveryGraceUsers   = await _db.UserSubscriptions.CountAsync(x => x.Status == "GracePeriod");

            return Ok(new
            {
                Creators        = creatorCount,
                CreatorProfiles = creatorProfileCount,
                LinkedChannels  = channelCount,
                Users           = userCount,
                Collaborations  = collaborationCount,
                WaitlistPending = waitlistPendingCount,
                EnterpriseLeads = enterpriseLeadsCount,
                RecoveryGraceUsers = recoveryGraceUsers,
                QuotaUsedToday  = _quota.UsedToday,
                QuotaDailyLimit = _quota.DailyLimit,
                Timestamp       = DateTime.UtcNow
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? query = null,
            [FromQuery] string? role = null,
            [FromQuery] string? customerType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            var usersQuery = _db.Users.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                usersQuery = usersQuery.Where(u =>
                    u.Name.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(role))
            {
                usersQuery = usersQuery.Where(u => u.Role == role);
            }

            if (!string.IsNullOrWhiteSpace(customerType))
            {
                usersQuery = usersQuery.Where(u => u.CustomerType == customerType);
            }

            var total = await usersQuery.CountAsync();
            var rows = await usersQuery
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    u.Email,
                    u.Role,
                    u.CustomerType,
                    u.EmailVerified,
                    u.Country,
                    u.CompanyName,
                    u.AuthProvider,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new { page, pageSize, total, items = rows });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPatch("users/{userId:int}")]
        public async Task<IActionResult> UpdateUserBySuperAdmin(int userId, [FromBody] AdminUpdateUserDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                return NotFound(new { error = "User not found." });
            }

            var currentEmail = User?.Identity?.Name ?? string.Empty;
            if (string.Equals(user.Email, currentEmail, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(dto.Role)
                && !string.Equals(dto.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "You cannot remove your own SuperAdmin access." });
            }

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                user.Name = dto.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.Role))
            {
                user.Role = dto.Role.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.CustomerType))
            {
                user.CustomerType = dto.CustomerType.Trim();
            }

            if (dto.EmailVerified.HasValue)
            {
                user.EmailVerified = dto.EmailVerified.Value;
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                updated = true,
                user.UserId,
                user.Name,
                user.Email,
                user.Role,
                user.CustomerType,
                user.EmailVerified
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("subscription-recovery")]
        public async Task<IActionResult> GetSubscriptionRecoveryQueue(
            [FromQuery] bool graceOnly = true,
            [FromQuery] string? paymentStatus = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.UserSubscriptions
                .AsNoTracking()
                .Include(x => x.User)
                .Include(x => x.Plan)
                .AsQueryable();

            if (graceOnly)
            {
                query = query.Where(x => x.Status == "GracePeriod");
            }

            if (!string.IsNullOrWhiteSpace(paymentStatus))
            {
                query = query.Where(x => x.PaymentStatus == paymentStatus);
            }

            var total = await query.CountAsync();
            var now = DateTime.UtcNow;

            var rows = await query
                .OrderByDescending(x => x.GracePeriodEndsAt ?? x.EndDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.SubscriptionId,
                    x.UserId,
                    UserName = x.User != null ? x.User.Name : string.Empty,
                    UserEmail = x.User != null ? x.User.Email : string.Empty,
                    PlanName = x.Plan != null ? x.Plan.PlanName : string.Empty,
                    x.Status,
                    x.PaymentStatus,
                    x.EndDate,
                    x.GracePeriodEndsAt,
                    x.PaymentRetryCount,
                    x.LastPaymentRetryAt,
                    x.PaymentMethodBrand,
                    x.PaymentMethodLast4
                })
                .ToListAsync();

            var items = rows.Select(x => new
            {
                x.SubscriptionId,
                x.UserId,
                x.UserName,
                x.UserEmail,
                x.PlanName,
                x.Status,
                x.PaymentStatus,
                x.EndDate,
                x.GracePeriodEndsAt,
                x.PaymentRetryCount,
                x.LastPaymentRetryAt,
                PaymentMethodDisplay = string.IsNullOrWhiteSpace(x.PaymentMethodLast4)
                    ? null
                    : $"{x.PaymentMethodBrand ?? "Card"} ****{x.PaymentMethodLast4}",
                GraceHoursRemaining = x.GracePeriodEndsAt.HasValue
                    ? Math.Round((x.GracePeriodEndsAt.Value - now).TotalHours, 1)
                    : (double?)null,
                GraceDaysRemaining = x.GracePeriodEndsAt.HasValue
                    ? Math.Max(0, (int)Math.Ceiling((x.GracePeriodEndsAt.Value - now).TotalDays))
                    : (int?)null,
            }).ToList();

            return Ok(new
            {
                page,
                pageSize,
                total,
                items
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("subscription-recovery/{subscriptionId:int}/outreach")]
        public async Task<IActionResult> TriggerSubscriptionRecoveryOutreach(int subscriptionId, [FromBody] SubscriptionRecoveryOutreachDto? dto)
        {
            var subscription = await _db.UserSubscriptions
                .Include(x => x.User)
                .Include(x => x.Plan)
                .FirstOrDefaultAsync(x => x.SubscriptionId == subscriptionId);

            if (subscription == null || subscription.User == null)
            {
                return NotFound(new { error = "Subscription not found." });
            }

            var message = string.IsNullOrWhiteSpace(dto?.Message)
                ? "We noticed a billing issue. Please update your payment method and retry payment to avoid downgrade."
                : dto!.Message.Trim();

            var title = "Payment recovery reminder";
            var type = $"admin.recovery.outreach.{subscription.SubscriptionId}.{DateTime.UtcNow:yyyyMMddHHmmss}";

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = subscription.UserId,
                Type = type,
                Title = title,
                Message = message,
                SendEmail = true
            });

            return Ok(new
            {
                sent = true,
                subscription.SubscriptionId,
                subscription.UserId,
                subscription.Status,
                subscription.PaymentStatus,
                subscription.GracePeriodEndsAt,
                title,
                message
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("brand-waitlist")]
        public async Task<IActionResult> GetBrandWaitlist(
            [FromQuery] string? status,
            [FromQuery] string? customerType,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.BrandWaitlistEntries.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(customerType))
            {
                query = query.Where(x => x.CustomerType == customerType);
            }

            if (from.HasValue)
            {
                var fromUtc = NormalizeToUtc(from.Value);
                query = query.Where(x => x.CreatedAt >= fromUtc);
            }

            if (to.HasValue)
            {
                var toInclusive = NormalizeToUtc(to.Value.Date.AddDays(1).AddTicks(-1));
                query = query.Where(x => x.CreatedAt <= toInclusive);
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.BrandWaitlistEntryId,
                    x.Email,
                    x.CompanyName,
                    x.CustomerType,
                    x.Role,
                    x.Notes,
                    x.Status,
                    x.CreatedAt,
                    x.UserId
                })
                .ToListAsync();

            var statusSummary = await _db.BrandWaitlistEntries
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                items,
                statusSummary
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPatch("brand-waitlist/{id:int}/status")]
        public async Task<IActionResult> UpdateBrandWaitlistStatus(int id, [FromBody] UpdateBrandWaitlistStatusDto dto)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Pending",
                "Approved",
                "Rejected",
                "Contacted"
            };

            if (string.IsNullOrWhiteSpace(dto.Status) || !allowed.Contains(dto.Status))
            {
                return BadRequest(new { error = "Invalid status. Allowed: Pending, Approved, Rejected, Contacted." });
            }

            var entry = await _db.BrandWaitlistEntries.FirstOrDefaultAsync(x => x.BrandWaitlistEntryId == id);
            if (entry == null)
            {
                return NotFound(new { error = "Waitlist entry not found." });
            }

            entry.Status = allowed.First(x => string.Equals(x, dto.Status, StringComparison.OrdinalIgnoreCase));
            await _db.SaveChangesAsync();

            return Ok(new
            {
                updated = true,
                entry.BrandWaitlistEntryId,
                entry.Status
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("brand-waitlist/export")]
        public async Task<IActionResult> ExportBrandWaitlistCsv(
            [FromQuery] string? status,
            [FromQuery] string? customerType,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var query = _db.BrandWaitlistEntries.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(customerType))
            {
                query = query.Where(x => x.CustomerType == customerType);
            }

            if (from.HasValue)
            {
                var fromUtc = NormalizeToUtc(from.Value);
                query = query.Where(x => x.CreatedAt >= fromUtc);
            }

            if (to.HasValue)
            {
                var toInclusive = NormalizeToUtc(to.Value.Date.AddDays(1).AddTicks(-1));
                query = query.Where(x => x.CreatedAt <= toInclusive);
            }

            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.BrandWaitlistEntryId,
                    x.CreatedAt,
                    x.CompanyName,
                    x.Email,
                    x.CustomerType,
                    x.Role,
                    x.Status,
                    x.Notes,
                    x.UserId
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("EntryId,CreatedAtUtc,CompanyName,Email,CustomerType,Role,Status,Notes,UserId");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(',', new[]
                {
                    CsvEscape(row.BrandWaitlistEntryId.ToString()),
                    CsvEscape(row.CreatedAt.ToString("O")),
                    CsvEscape(row.CompanyName),
                    CsvEscape(row.Email),
                    CsvEscape(row.CustomerType),
                    CsvEscape(row.Role),
                    CsvEscape(row.Status),
                    CsvEscape(row.Notes ?? string.Empty),
                    CsvEscape(row.UserId?.ToString() ?? string.Empty),
                }));
            }

            var fileName = $"brand-waitlist-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }

        private static string CsvEscape(string value)
        {
            if (value.Contains('"'))
            {
                value = value.Replace("\"", "\"\"");
            }

            if (value.Contains(',') || value.Contains('\n') || value.Contains('\r') || value.Contains('"'))
            {
                return $"\"{value}\"";
            }

            return value;
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("enterprise-leads")]
        public async Task<IActionResult> GetEnterpriseLeads(
            [FromQuery] string? source,
            [FromQuery] string? status,
            [FromQuery] int? ownerUserId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.EnterpriseLeads.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(source))
            {
                query = query.Where(x => x.Source == source);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            if (ownerUserId.HasValue)
            {
                query = query.Where(x => x.OwnerUserId == ownerUserId.Value);
            }

            if (from.HasValue)
            {
                var fromUtc = NormalizeToUtc(from.Value);
                query = query.Where(x => x.CreatedAt >= fromUtc);
            }

            if (to.HasValue)
            {
                var toInclusive = NormalizeToUtc(to.Value.Date.AddDays(1).AddTicks(-1));
                query = query.Where(x => x.CreatedAt <= toInclusive);
            }

            var now = DateTime.UtcNow;
            var total = await query.CountAsync();
            var pageRows = await query
                .Include(x => x.OwnerUser)
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = pageRows.Select(x => new
            {
                x.EnterpriseLeadId,
                x.FullName,
                x.WorkEmail,
                x.CompanyName,
                x.TeamSize,
                x.Notes,
                x.Source,
                x.Status,
                x.OwnerUserId,
                OwnerName = x.OwnerUser != null ? x.OwnerUser.Name : null,
                x.CreatedAt,
                x.UpdatedAt,
                SlaHoursElapsed = Math.Round((now - x.CreatedAt).TotalHours, 1),
                SlaStatus = GetSlaStatus(now, x.CreatedAt)
            }).ToList();

            var summaryRows = await query.Select(x => x.CreatedAt).ToListAsync();
            var summary = new
            {
                Total = summaryRows.Count,
                Healthy = summaryRows.Count(x => (now - x).TotalHours <= 24),
                AtRisk = summaryRows.Count(x => (now - x).TotalHours > 24 && (now - x).TotalHours <= 48),
                Breached = summaryRows.Count(x => (now - x).TotalHours > 48),
            };

            return Ok(new
            {
                page,
                pageSize,
                total,
                summary,
                items
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("enterprise-lead-owners")]
        public async Task<IActionResult> GetEnterpriseLeadOwners()
        {
            var owners = await _db.Users
                .AsNoTracking()
                .Where(x => x.Role == "SuperAdmin")
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.UserId,
                    x.Name,
                    x.Email
                })
                .ToListAsync();

            return Ok(owners);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPatch("enterprise-leads/{id:int}")]
        public async Task<IActionResult> UpdateEnterpriseLead(int id, [FromBody] UpdateEnterpriseLeadDto dto)
        {
            var lead = await _db.EnterpriseLeads.FirstOrDefaultAsync(x => x.EnterpriseLeadId == id);
            if (lead == null)
            {
                return NotFound(new { error = "Enterprise lead not found." });
            }

            var actorUserId = GetUserId();
            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                var normalizedStatus = NormalizeLeadStatus(dto.Status);
                if (normalizedStatus == null)
                {
                    return BadRequest(new { error = "Invalid status. Allowed: New, Contacted, Qualified, ClosedWon, ClosedLost." });
                }

                if (!string.Equals(lead.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    _db.EnterpriseLeadActivities.Add(new EnterpriseLeadActivity
                    {
                        EnterpriseLeadId = lead.EnterpriseLeadId,
                        ActivityType = "StatusChanged",
                        Message = $"Status changed from {lead.Status} to {normalizedStatus}.",
                        ActorUserId = actorUserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                lead.Status = normalizedStatus;
            }

            if (dto.OwnerUserId.HasValue)
            {
                if (dto.OwnerUserId.Value <= 0)
                {
                    lead.OwnerUserId = null;
                }
                else
                {
                    var owner = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == dto.OwnerUserId.Value && x.Role == "SuperAdmin");
                    if (owner == null)
                    {
                        return BadRequest(new { error = "Owner must be a valid SuperAdmin user." });
                    }

                    if (lead.OwnerUserId != owner.UserId)
                    {
                        _db.EnterpriseLeadActivities.Add(new EnterpriseLeadActivity
                        {
                            EnterpriseLeadId = lead.EnterpriseLeadId,
                            ActivityType = "OwnerChanged",
                            Message = $"Owner updated to {owner.Name}.",
                            ActorUserId = actorUserId,
                            CreatedAt = DateTime.UtcNow
                        });

                        await _notifications.NotifyAsync(new NotificationCreateRequestDto
                        {
                            UserId = owner.UserId,
                            Type = "lead.assigned",
                            Title = "Enterprise lead assigned",
                            Message = $"Lead {lead.FullName} ({lead.CompanyName}) has been assigned to you.",
                            SendEmail = false
                        });
                    }

                    lead.OwnerUserId = owner.UserId;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Notes))
            {
                _db.EnterpriseLeadActivities.Add(new EnterpriseLeadActivity
                {
                    EnterpriseLeadId = lead.EnterpriseLeadId,
                    ActivityType = "Comment",
                    Message = dto.Notes.Trim(),
                    ActorUserId = actorUserId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            lead.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { updated = true, lead.EnterpriseLeadId, lead.Status, lead.OwnerUserId, lead.UpdatedAt });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("enterprise-leads/{id:int}/timeline")]
        public async Task<IActionResult> GetEnterpriseLeadTimeline(int id)
        {
            var exists = await _db.EnterpriseLeads.AsNoTracking().AnyAsync(x => x.EnterpriseLeadId == id);
            if (!exists)
            {
                return NotFound(new { error = "Enterprise lead not found." });
            }

            var items = await _db.EnterpriseLeadActivities
                .AsNoTracking()
                .Where(x => x.EnterpriseLeadId == id)
                .Include(x => x.ActorUser)
                .OrderByDescending(x => x.CreatedAt)
                .Take(80)
                .Select(x => new
                {
                    x.EnterpriseLeadActivityId,
                    x.ActivityType,
                    x.Message,
                    x.ActorUserId,
                    ActorName = x.ActorUser != null ? x.ActorUser.Name : null,
                    x.CreatedAt
                })
                .ToListAsync();

            return Ok(new { items });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("enterprise-leads/{id:int}/auto-assign")]
        public async Task<IActionResult> AutoAssignEnterpriseLead(int id)
        {
            var lead = await _db.EnterpriseLeads.FirstOrDefaultAsync(x => x.EnterpriseLeadId == id);
            if (lead == null)
            {
                return NotFound(new { error = "Enterprise lead not found." });
            }

            var owners = await _db.Users
                .AsNoTracking()
                .Where(x => x.Role == "SuperAdmin")
                .Select(x => new { x.UserId, x.Name })
                .ToListAsync();

            if (owners.Count == 0)
            {
                return BadRequest(new { error = "No SuperAdmin owners are available for assignment." });
            }

            var openStatuses = new[] { "New", "Contacted", "Qualified" };
            var counts = await _db.EnterpriseLeads
                .AsNoTracking()
                .Where(x => x.OwnerUserId != null && openStatuses.Contains(x.Status))
                .GroupBy(x => x.OwnerUserId!.Value)
                .Select(g => new { OwnerUserId = g.Key, Count = g.Count() })
                .ToListAsync();

            var selected = owners
                .Select(o => new
                {
                    o.UserId,
                    o.Name,
                    Count = counts.FirstOrDefault(c => c.OwnerUserId == o.UserId)?.Count ?? 0
                })
                .OrderBy(x => x.Count)
                .ThenBy(x => x.UserId)
                .First();

            lead.OwnerUserId = selected.UserId;
            lead.UpdatedAt = DateTime.UtcNow;

            _db.EnterpriseLeadActivities.Add(new EnterpriseLeadActivity
            {
                EnterpriseLeadId = lead.EnterpriseLeadId,
                ActivityType = "AutoAssigned",
                Message = $"Auto-assigned to {selected.Name}.",
                ActorUserId = GetUserId(),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            await _notifications.NotifyAsync(new NotificationCreateRequestDto
            {
                UserId = selected.UserId,
                Type = "lead.auto_assigned",
                Title = "Enterprise lead auto-assigned",
                Message = $"Lead {lead.FullName} ({lead.CompanyName}) has been auto-assigned to you.",
                SendEmail = false
            });

            return Ok(new { assigned = true, lead.EnterpriseLeadId, lead.OwnerUserId, OwnerName = selected.Name, lead.UpdatedAt });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("enterprise-leads/export")]
        public async Task<IActionResult> ExportEnterpriseLeadsCsv(
            [FromQuery] string? source,
            [FromQuery] string? status,
            [FromQuery] int? ownerUserId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var query = _db.EnterpriseLeads.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(source))
            {
                query = query.Where(x => x.Source == source);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            if (ownerUserId.HasValue)
            {
                query = query.Where(x => x.OwnerUserId == ownerUserId.Value);
            }

            if (from.HasValue)
            {
                var fromUtc = NormalizeToUtc(from.Value);
                query = query.Where(x => x.CreatedAt >= fromUtc);
            }

            if (to.HasValue)
            {
                var toInclusive = NormalizeToUtc(to.Value.Date.AddDays(1).AddTicks(-1));
                query = query.Where(x => x.CreatedAt <= toInclusive);
            }

            var now = DateTime.UtcNow;
            var rows = await query
                .Include(x => x.OwnerUser)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.EnterpriseLeadId,
                    x.CreatedAt,
                    x.FullName,
                    x.WorkEmail,
                    x.CompanyName,
                    x.TeamSize,
                    x.Source,
                    x.Status,
                    OwnerName = x.OwnerUser != null ? x.OwnerUser.Name : string.Empty,
                    x.Notes
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("LeadId,CreatedAtUtc,FullName,WorkEmail,CompanyName,TeamSize,Source,Status,Owner,SlaStatus,Notes");

            foreach (var row in rows)
            {
                csv.AppendLine(string.Join(',', new[]
                {
                    CsvEscape(row.EnterpriseLeadId.ToString()),
                    CsvEscape(row.CreatedAt.ToString("O")),
                    CsvEscape(row.FullName),
                    CsvEscape(row.WorkEmail),
                    CsvEscape(row.CompanyName),
                    CsvEscape(row.TeamSize ?? string.Empty),
                    CsvEscape(row.Source),
                    CsvEscape(row.Status),
                    CsvEscape(row.OwnerName),
                    CsvEscape(GetSlaStatus(now, row.CreatedAt)),
                    CsvEscape(row.Notes ?? string.Empty),
                }));
            }

            var fileName = $"enterprise-leads-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }

        private static string GetSlaStatus(DateTime now, DateTime createdAt)
        {
            var hours = (now - createdAt).TotalHours;
            if (hours > 48) return "Breached";
            if (hours > 24) return "At Risk";
            return "Healthy";
        }

        private static string? NormalizeLeadStatus(string status)
        {
            return status.Trim().ToLowerInvariant() switch
            {
                "new" => "New",
                "contacted" => "Contacted",
                "qualified" => "Qualified",
                "closedwon" => "ClosedWon",
                "closedlost" => "ClosedLost",
                _ => null
            };
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("creator-profiles")]
        public async Task<IActionResult> GetCreatorProfiles(
            [FromQuery] string? query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            var q = _db.CreatorProfiles
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Channels)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                q = q.Where(p =>
                    (p.User.Name ?? string.Empty).ToLower().Contains(term) ||
                    (p.User.Email ?? string.Empty).ToLower().Contains(term) ||
                    (p.Category ?? string.Empty).ToLower().Contains(term) ||
                    p.Channels.Any(c => (c.ChannelName ?? string.Empty).ToLower().Contains(term) || c.ChannelId.ToLower().Contains(term)));
            }

            var total = await q.CountAsync();
            var rows = await q
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.CreatorProfileId,
                    p.UserId,
                    UserName = p.User.Name,
                    UserEmail = p.User.Email,
                    p.User.Role,
                    p.User.CustomerType,
                    p.Country,
                    p.Language,
                    p.Category,
                    Channel = p.Channels
                        .OrderByDescending(c => c.LastStatsUpdatedAt ?? c.CreatedAt)
                        .Select(c => new
                        {
                            c.ChannelId,
                            c.ChannelName,
                            c.Subscribers,
                            c.EngagementRate,
                            c.LastStatsUpdatedAt
                        })
                        .FirstOrDefault(),
                    Creator = _db.Creators
                        .Where(c => c.UserId == p.UserId)
                        .Select(c => new { c.CreatorId, c.LastRefreshedAt })
                        .FirstOrDefault(),
                    Score = _db.CreatorScores
                        .Where(s => s.Creator.UserId == p.UserId)
                        .Select(s => (double?)s.Score)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                items = rows.Select(r => new
                {
                    r.CreatorProfileId,
                    r.UserId,
                    r.UserName,
                    r.UserEmail,
                    r.Role,
                    r.CustomerType,
                    r.Country,
                    r.Language,
                    r.Category,
                    channelId = r.Channel?.ChannelId,
                    channelName = r.Channel?.ChannelName,
                    subscribers = r.Channel?.Subscribers,
                    engagementRate = r.Channel?.EngagementRate,
                    channelLastUpdatedAt = r.Channel?.LastStatsUpdatedAt,
                    creatorId = r.Creator?.CreatorId,
                    creatorLastRefreshedAt = r.Creator?.LastRefreshedAt,
                    aiScore = r.Score
                })
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("creator-profiles/{creatorProfileId:int}")]
        public async Task<IActionResult> GetCreatorProfileDetail(int creatorProfileId)
        {
            var profile = await _db.CreatorProfiles
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Channels)
                .FirstOrDefaultAsync(p => p.CreatorProfileId == creatorProfileId);

            if (profile == null)
            {
                return NotFound(new { error = "Creator profile not found." });
            }

            var channelIds = profile.Channels.Select(c => c.ChannelId).ToList();
            var recentVideos = channelIds.Count == 0
                ? new List<object>()
                : await _db.ChannelVideos
                    .AsNoTracking()
                    .Where(v => channelIds.Contains(v.ChannelId))
                    .OrderByDescending(v => v.PublishedAt)
                    .Take(10)
                    .Select(v => (object)new
                    {
                        v.YoutubeVideoId,
                        v.Title,
                        v.ThumbnailUrl,
                        v.ViewCount,
                        v.LikeCount,
                        v.CommentCount,
                        v.PublishedAt,
                        v.ChannelId
                    })
                    .ToListAsync();

            var creator = await _db.Creators
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == profile.UserId);

            var aiScore = creator == null
                ? null
                : await _db.CreatorScores
                    .AsNoTracking()
                    .Where(s => s.CreatorId == creator.CreatorId)
                    .Select(s => new { s.Score })
                    .FirstOrDefaultAsync();

            return Ok(new
            {
                profile = new
                {
                    profile.CreatorProfileId,
                    profile.UserId,
                    userName = profile.User.Name,
                    userEmail = profile.User.Email,
                    role = profile.User.Role,
                    customerType = profile.User.CustomerType,
                    profile.Country,
                    profile.Language,
                    profile.Category,
                    profile.InstagramHandle,
                    profile.ContactEmail,
                    profile.Bio,
                    profile.CreatedAt
                },
                channels = profile.Channels.Select(c => new
                {
                    c.ChannelId,
                    c.ChannelName,
                    c.ChannelUrl,
                    c.Subscribers,
                    c.TotalViews,
                    c.VideoCount,
                    c.EngagementRate,
                    c.LastStatsUpdatedAt
                }),
                creator = creator == null ? null : new
                {
                    creator.CreatorId,
                    creator.ChannelId,
                    creator.ChannelName,
                    creator.ThumbnailUrl,
                    creator.LastRefreshedAt
                },
                ai = aiScore,
                recentVideos
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPut("creator-profiles/{creatorProfileId:int}")]
        public async Task<IActionResult> UpdateCreatorProfileByAdmin(int creatorProfileId, [FromBody] AdminUpdateCreatorProfileDto dto)
        {
            var profile = await _db.CreatorProfiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.CreatorProfileId == creatorProfileId);

            if (profile == null)
            {
                return NotFound(new { error = "Creator profile not found." });
            }

            if (!string.IsNullOrWhiteSpace(dto.UserName)) profile.User.Name = dto.UserName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.UserEmail)) profile.User.Email = dto.UserEmail.Trim().ToLowerInvariant();
            if (dto.Country != null) profile.Country = dto.Country;
            if (dto.Language != null) profile.Language = dto.Language;
            if (dto.Category != null) profile.Category = dto.Category;
            if (dto.InstagramHandle != null) profile.InstagramHandle = dto.InstagramHandle;
            if (dto.ContactEmail != null) profile.ContactEmail = dto.ContactEmail;
            if (dto.Bio != null) profile.Bio = dto.Bio;

            await _db.SaveChangesAsync();

            return Ok(new { updated = true, profile.CreatorProfileId, profile.UserId, profile.User.Name, profile.User.Email });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("creator-profiles/{creatorProfileId:int}/refresh-ai")]
        public async Task<IActionResult> RefreshCreatorAiSignals(int creatorProfileId, CancellationToken ct)
        {
            var profile = await _db.CreatorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.CreatorProfileId == creatorProfileId, ct);

            if (profile == null)
            {
                return NotFound(new { error = "Creator profile not found." });
            }

            var creator = await _db.Creators
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == profile.UserId, ct);

            if (creator == null)
            {
                return BadRequest(new { error = "Creator AI index row not found. Link/import channel first." });
            }

            await _analytics.RefreshAnalyticsAsync(creator.CreatorId);
            await _videoAnalytics.RefreshCreatorAsync(creator.CreatorId, ct);

            return Ok(new
            {
                refreshed = true,
                creator.CreatorId,
                creator.ChannelName,
                timestamp = DateTime.UtcNow
            });
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpDelete("creator-profiles/{creatorProfileId:int}")]
        public async Task<IActionResult> DeleteCreatorProfileByAdmin(int creatorProfileId)
        {
            var profile = await _db.CreatorProfiles
                .Include(p => p.Channels)
                .FirstOrDefaultAsync(p => p.CreatorProfileId == creatorProfileId);

            if (profile == null)
            {
                return NotFound(new { error = "Creator profile not found." });
            }

            var creator = await _db.Creators.FirstOrDefaultAsync(c => c.UserId == profile.UserId);
            if (creator != null)
            {
                _db.Creators.Remove(creator);
            }

            _db.CreatorProfiles.Remove(profile);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                deleted = true,
                creatorProfileId,
                message = "Creator profile removed from creator workflows. User account remains for audit continuity."
            });
        }

        private int GetUserId()
            => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

        private static DateTime NormalizeToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

            public class AdminUpdateCreatorProfileDto
            {
                public string? UserName { get; set; }
                public string? UserEmail { get; set; }
                public string? Country { get; set; }
                public string? Language { get; set; }
                public string? Category { get; set; }
                public string? InstagramHandle { get; set; }
                public string? ContactEmail { get; set; }
                public string? Bio { get; set; }
            }

        // ── Jobs ──────────────────────────────────────────────────────────────
        // Each job is wrapped in a 3-minute hard timeout so a hung network call
        // never leaves the button spinning forever.
        private static CancellationTokenSource JobTimeout(CancellationToken ct, int minutes = 3)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(minutes));
            return cts;
        }

        private static IActionResult TimedOut(string job)
            => new ObjectResult(new { job, error = "Job timed out after 3 minutes.", timestamp = DateTime.UtcNow })
               { StatusCode = 408 };

        /// <summary>Refresh creator analytics (avg views/likes/comments) + growth snapshots.</summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/analytics")]
        public async Task<IActionResult> RunAnalytics(CancellationToken ct)
        {
            try
            {
                using var cts = JobTimeout(ct);
                await _analytics.RecordGrowthSnapshotAsync();
                await _analytics.RefreshAllAnalyticsAsync();
                return Ok(new { job = "analytics", timestamp = DateTime.UtcNow });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return TimedOut("analytics"); }
        }

        /// <summary>Run language detection for all creators.</summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/language")]
        public async Task<IActionResult> RunLanguage(CancellationToken ct)
        {
            try
            {
                using var cts = JobTimeout(ct);
                await _language.RefreshAllAsync(cts.Token);
                return Ok(new { job = "language", timestamp = DateTime.UtcNow });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return TimedOut("language"); }
        }

        /// <summary>Run viral content scoring for all creators.</summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/viral")]
        public async Task<IActionResult> RunViral(CancellationToken ct)
        {
            try
            {
                using var cts = JobTimeout(ct);
                await _viral.RefreshViralScoresAsync(cts.Token);
                return Ok(new { job = "viral", timestamp = DateTime.UtcNow });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return TimedOut("viral"); }
        }

        /// <summary>Recalculate rising-creator growth scores.</summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/rising")]
        public async Task<IActionResult> RunRising(CancellationToken ct)
        {
            try
            {
                using var cts = JobTimeout(ct);
                await _rising.RecalculateAllGrowthScoresAsync(cts.Token);
                return Ok(new { job = "rising", timestamp = DateTime.UtcNow });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return TimedOut("rising"); }
        }

        /// <summary>Recalculate creator scores + scan all creators for brand mentions.</summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/marketing")]
        public async Task<IActionResult> RunMarketing(CancellationToken ct)
        {
            try
            {
                using var cts = JobTimeout(ct);
                await _scoring.RecalculateAllScoresAsync(cts.Token);
                await _brand.ScanAllCreatorsAsync(cts.Token);
                return Ok(new { job = "marketing", timestamp = DateTime.UtcNow });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return TimedOut("marketing"); }
        }

        /// <summary>
        /// Refresh YouTube channel stats (subscribers/views/videoCount) for all
        /// Feature-7 registered creator channels.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/creator-stats")]
        public async Task<IActionResult> RunCreatorStats(CancellationToken ct)
        {
            var channelIds = await _db.CreatorChannels
                .Select(c => c.ChannelId)
                .ToListAsync(ct);

            var refreshed = 0;
            try
            {
                using var cts = JobTimeout(ct);
                foreach (var id in channelIds)
                {
                    try
                    {
                        await _channel.RefreshChannelStatsAsync(id, cts.Token);
                        refreshed++;
                    }
                    catch (OperationCanceledException) { break; }
                    catch { /* log already inside service */ }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return StatusCode(408, new { job = "creator-stats", refreshed, error = "Timed out after 3 minutes.", timestamp = DateTime.UtcNow });
            }
            return Ok(new { job = "creator-stats", refreshed, timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Fetch live YouTube data (subscribers, views, thumbnail, recent videos) for all
        /// legacy Influencer records and store it in the in-memory LegacyChannelCache.
        /// The Marketplace controller reads from this cache, so no YouTube API calls are
        /// made automatically on page load.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/legacy-channels")]
        public async Task<IActionResult> RunLegacyChannels(CancellationToken ct)
        {
            var influencers = await _db.Influencers
                .Where(i => i.YouTubeLink != null && i.YouTubeLink != string.Empty)
                .Select(i => new { i.InfluencerId, i.YouTubeLink })
                .ToListAsync(ct);

            var results = new System.Collections.Generic.List<object>();
            var fetched = 0;
            var failed  = 0;

            try
            {
                using var cts = JobTimeout(ct, minutes: 5);
                foreach (var inf in influencers)
                {
                    try
                    {
                        var snap = await _channel.FetchLiveChannelByUrlAsync(inf.YouTubeLink!, cts.Token);
                        if (snap != null)
                        {
                            _legacyCache.SetSnapshot(inf.YouTubeLink!, snap);
                            var videos = await _channel.GetRecentVideosAsync(snap.ChannelId, 10, cts.Token);
                            _legacyCache.SetVideos(snap.ChannelId, videos);
                            fetched++;
                            results.Add(new { id = inf.InfluencerId, url = inf.YouTubeLink, status = "ok", channel = snap.ChannelName, subscribers = snap.Subscribers });
                        }
                        else
                        {
                            failed++;
                            results.Add(new { id = inf.InfluencerId, url = inf.YouTubeLink, status = "null_returned", detail = "FetchLiveChannelByUrlAsync returned null — check API key, quota, or URL format" });
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        failed++;
                        results.Add(new { id = inf.InfluencerId, url = inf.YouTubeLink, status = "exception", detail = ex.Message });
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return StatusCode(408, new { job = "legacy-channels", fetched, failed, cached = _legacyCache.Count, error = "Timed out after 5 minutes.", influencers = results, timestamp = DateTime.UtcNow });
            }
            return Ok(new
            {
                job            = "legacy-channels",
                fetched,
                failed,
                cached         = _legacyCache.Count,
                quotaUsedToday = _quota.UsedToday,
                quotaLimit     = _quota.DailyLimit,
                influencers    = results,
                timestamp      = DateTime.UtcNow
            });
        }

        // ── Feature 8: Video Analytics ──────────────────────────────────────────

        /// <summary>
        /// Fetch the 50 most-recent videos for every creator in the DB,
        /// compute per-video engagement rates, detect brand collaborations
        /// against the curated brand dictionary, and upsert results into
        /// the <c>VideoAnalytics</c> table.
        ///
        /// Hard timeout: 10 minutes.
        /// Stops early if YouTube returns 403 / 429 (quota exhausted).
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/video-analytics")]
        public async Task<IActionResult> RunVideoAnalytics(CancellationToken ct)
        {
            int upserted = 0;
            try
            {
                using var cts = JobTimeout(ct, minutes: 10);
                var total = await _db.Creators.CountAsync(cts.Token);
                await _videoAnalytics.RefreshAllAsync(cts.Token);
                upserted = await _db.VideoAnalytics.CountAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return StatusCode(408, new { job = "video-analytics", upserted, error = "Timed out after 10 minutes.", timestamp = DateTime.UtcNow });
            }
            return Ok(new
            {
                job            = "video-analytics",
                upserted,
                quotaUsedToday = _quota.UsedToday,
                quotaLimit     = _quota.DailyLimit,
                timestamp      = DateTime.UtcNow
            });
        }

        // ── Diagnostics ──────────────────────────────────────────────────────────

        /// <summary>
        /// Directly calls the YouTube Channels API for a given handle/URL and returns
        /// the raw HTTP status + body so you can diagnose API key or restriction issues.
        /// Example: GET /api/admin/debug/youtube?handle=KaranFromIITRopar
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("debug/youtube")]
        public async Task<IActionResult> DebugYouTube(
            [FromQuery] string? handle,
            [FromQuery] string? channelId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return BadRequest(new { error = "YouTube:ApiKey is not set in appsettings.json" });

            var client = _httpClientFactory.CreateClient();
            var results = new System.Collections.Generic.List<object>();

            // Test 1: forHandle lookup (new API, 2022+)
            if (!string.IsNullOrWhiteSpace(handle))
            {
                var h = handle.TrimStart('@');
                var url1 = $"https://www.googleapis.com/youtube/v3/channels?part=id,snippet&forHandle={Uri.EscapeDataString(h)}&key={_apiKey}";
                using var r1 = await client.GetAsync(url1, ct);
                var body1 = await r1.Content.ReadAsStringAsync(ct);
                results.Add(new { test = "forHandle", url = url1.Replace(_apiKey!, "[KEY]"), status = (int)r1.StatusCode, body = body1.Length > 500 ? body1[..500] : body1 });
            }

            // Test 2: forUsername lookup (legacy API)
            if (!string.IsNullOrWhiteSpace(handle))
            {
                var h = handle.TrimStart('@');
                var url2 = $"https://www.googleapis.com/youtube/v3/channels?part=id,snippet&forUsername={Uri.EscapeDataString(h)}&key={_apiKey}";
                using var r2 = await client.GetAsync(url2, ct);
                var body2 = await r2.Content.ReadAsStringAsync(ct);
                results.Add(new { test = "forUsername", url = url2.Replace(_apiKey!, "[KEY]"), status = (int)r2.StatusCode, body = body2.Length > 500 ? body2[..500] : body2 });
            }

            // Test 3: direct channel ID lookup
            if (!string.IsNullOrWhiteSpace(channelId))
            {
                var url3 = $"https://www.googleapis.com/youtube/v3/channels?part=id,snippet,statistics&id={Uri.EscapeDataString(channelId)}&key={_apiKey}";
                using var r3 = await client.GetAsync(url3, ct);
                var body3 = await r3.Content.ReadAsStringAsync(ct);
                results.Add(new { test = "byChannelId", url = url3.Replace(_apiKey!, "[KEY]"), status = (int)r3.StatusCode, body = body3.Length > 500 ? body3[..500] : body3 });
            }

            return Ok(new { apiKeyConfigured = !string.IsNullOrWhiteSpace(_apiKey), apiKeyPrefix = _apiKey?.Length > 8 ? _apiKey[..8] + "..." : "too short", tests = results });
        }

        // ── YouTube Creator Import ─────────────────────────────────────────────

        /// <summary>
        /// Search YouTube for channels matching a query and upsert them into the Creators table.
        /// Parses emails and social handles from channel descriptions.
        /// Requires YouTube:ApiKey to be set in appsettings.json.
        ///
        /// This job is manual only and never runs automatically.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/youtube-import")]
        public async Task<IActionResult> RunYouTubeImport(
            [FromBody] YouTubeImportRequestDto dto,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return BadRequest(new { error = "YouTube:ApiKey is not configured in appsettings.json. Add it to run this job." });

            if (string.IsNullOrWhiteSpace(dto.Query))
                return BadRequest(new { error = "Query is required." });

            try
            {
                using var cts = JobTimeout(ct, minutes: 5);
                var result = await _youTubeImport.ImportAsync(dto, cts.Token);
                return Ok(result);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return TimedOut("youtube-import");
            }
        }

        /// <summary>
        /// One-time data hygiene job for legacy importer rows.
        /// Clears invalid/false-positive Instagram and Twitter handles from Creators.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost("jobs/cleanup-invalid-social-handles")]
        public async Task<IActionResult> CleanupInvalidSocialHandles(CancellationToken ct = default)
        {
            var creators = await _db.Creators
                .Where(c => c.InstagramHandle != null || c.TwitterHandle != null)
                .ToListAsync(ct);

            var scanned = 0;
            var affectedRows = 0;
            var instagramCleared = 0;
            var twitterCleared = 0;

            foreach (var creator in creators)
            {
                scanned++;

                var originalIg = creator.InstagramHandle;
                var originalTw = creator.TwitterHandle;

                var normalizedIg = NormalizeInstagramHandle(originalIg);
                var normalizedTw = NormalizeTwitterHandle(originalTw);

                if (!string.Equals(originalIg, normalizedIg, StringComparison.Ordinal))
                {
                    creator.InstagramHandle = normalizedIg;
                    if (originalIg != null && normalizedIg == null) instagramCleared++;
                }

                if (!string.Equals(originalTw, normalizedTw, StringComparison.Ordinal))
                {
                    creator.TwitterHandle = normalizedTw;
                    if (originalTw != null && normalizedTw == null) twitterCleared++;
                }

                if (!string.Equals(originalIg, creator.InstagramHandle, StringComparison.Ordinal)
                    || !string.Equals(originalTw, creator.TwitterHandle, StringComparison.Ordinal))
                {
                    affectedRows++;
                }
            }

            if (affectedRows > 0)
            {
                await _db.SaveChangesAsync(ct);
            }

            return Ok(new
            {
                scanned,
                affectedRows,
                instagramCleared,
                twitterCleared,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Browse all creators currently in the database (paginated, filterable).
        /// Used by the SuperAdmin YouTube importer results view.
        /// </summary>
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet("creators")]
        public async Task<IActionResult> GetCreators(
            [FromQuery] string? search,
            [FromQuery] string? category,
            [FromQuery] string? country,
            [FromQuery] string? tier,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.Creators.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    c.ChannelName.ToLower().Contains(s) ||
                    (c.Description != null && c.Description.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(c => c.Category == category);

            if (!string.IsNullOrWhiteSpace(country))
                query = query.Where(c => c.Country == country);

            if (!string.IsNullOrWhiteSpace(tier))
                query = query.Where(c => c.CreatorTier == tier);

            var total = await query.CountAsync(ct);
            var rows = await query
                .OrderByDescending(c => c.Subscribers)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.CreatorId,
                    c.ChannelId,
                    c.ChannelName,
                    c.Subscribers,
                    c.TotalViews,
                    c.VideoCount,
                    c.Category,
                    c.Country,
                    c.CreatorTier,
                    c.IsSmallCreator,
                    c.Language,
                    c.Region,
                    c.ThumbnailUrl,
                    c.ChannelUrl,
                    c.EngagementRate,
                    c.AvgViews,
                    c.AvgLikes,
                    c.AvgComments,
                    c.PublicEmail,
                    c.InstagramHandle,
                    c.TwitterHandle,
                    c.LastRefreshedAt,
                    c.CreatedAt,
                    c.UpdatedAt,
                })
                .ToListAsync(ct);

            return Ok(new { page, pageSize, total, items = rows });
        }

        private static string? NormalizeInstagramHandle(string? handle)
        {
            if (string.IsNullOrWhiteSpace(handle)) return null;

            var h = handle.Trim().TrimStart('@').ToLowerInvariant();
            if (!Regex.IsMatch(h, @"^[a-z0-9._]{2,30}$")) return null;
            if (h.StartsWith('.') || h.EndsWith('.')) return null;

            // Common false positives from email parsing
            if (h == "gmail" || h == "yahoo" || h == "hotmail" || h == "outlook" || h == "protonmail") return null;
            if (h.EndsWith(".com") || h.EndsWith(".net") || h.EndsWith(".org") || h.EndsWith(".in") || h.EndsWith(".co")) return null;

            return h;
        }

        private static string? NormalizeTwitterHandle(string? handle)
        {
            if (string.IsNullOrWhiteSpace(handle)) return null;

            var h = handle.Trim().TrimStart('@').ToLowerInvariant();
            if (!Regex.IsMatch(h, @"^[a-z0-9_]{2,15}$")) return null;

            return h;
        }
    }

    public class SeedAdminDto
    {
        public string? Name     { get; set; }
        public string  Email    { get; set; } = string.Empty;
        public string  Password { get; set; } = string.Empty;
    }

    public class UpdateBrandWaitlistStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class UpdateEnterpriseLeadDto
    {
        public string? Status { get; set; }
        public int? OwnerUserId { get; set; }
        public string? Notes { get; set; }
    }

    public class AdminUpdateUserDto
    {
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? CustomerType { get; set; }
        public bool? EmailVerified { get; set; }
    }

    public class SubscriptionRecoveryOutreachDto
    {
        public string? Message { get; set; }
    }
}
