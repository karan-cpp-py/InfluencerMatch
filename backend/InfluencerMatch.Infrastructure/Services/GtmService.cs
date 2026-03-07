using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Domain.Entities;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Services
{
    public class GtmService : IGtmService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public GtmService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<BookDemoLeadDto> BookDemoAsync(int? userId, BookDemoLeadDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.WorkEmail) || string.IsNullOrWhiteSpace(dto.CompanyName))
            {
                throw new InvalidOperationException("Name, work email and company name are required.");
            }

            var normalizedEmail = dto.WorkEmail.Trim().ToLowerInvariant();

            var lead = new EnterpriseLead
            {
                FullName = dto.FullName.Trim(),
                WorkEmail = normalizedEmail,
                CompanyName = dto.CompanyName.Trim(),
                TeamSize = string.IsNullOrWhiteSpace(dto.TeamSize) ? null : dto.TeamSize.Trim(),
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
                Source = string.IsNullOrWhiteSpace(dto.Source) ? "BookDemo" : dto.Source.Trim(),
                Status = "New",
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.EnterpriseLeads.Add(lead);

            await _db.SaveChangesAsync();

            _db.EnterpriseLeadActivities.Add(new EnterpriseLeadActivity
            {
                EnterpriseLeadId = lead.EnterpriseLeadId,
                ActivityType = "Created",
                Message = $"Lead created from source '{lead.Source}'.",
                ActorUserId = userId,
                CreatedAt = DateTime.UtcNow
            });

            var owner = await SelectAutoOwnerAsync();
            if (owner != null)
            {
                lead.OwnerUserId = owner.UserId;
                lead.UpdatedAt = DateTime.UtcNow;

                _db.EnterpriseLeadActivities.Add(new EnterpriseLeadActivity
                {
                    EnterpriseLeadId = lead.EnterpriseLeadId,
                    ActivityType = "AutoAssigned",
                    Message = $"Auto-assigned to {owner.Name}.",
                    CreatedAt = DateTime.UtcNow
                });

                await _notifications.NotifyAsync(new NotificationCreateRequestDto
                {
                    UserId = owner.UserId,
                    Type = "lead.auto_assigned",
                    Title = "New enterprise lead assigned",
                    Message = $"{lead.FullName} ({lead.CompanyName}) was auto-assigned to you.",
                    SendEmail = false
                });
            }

            await _db.SaveChangesAsync();

            dto.WorkEmail = normalizedEmail;
            return dto;
        }

        private async Task<User?> SelectAutoOwnerAsync()
        {
            var owners = await _db.Users
                .AsNoTracking()
                .Where(x => x.Role == "SuperAdmin")
                .Select(x => new { x.UserId, x.Name })
                .ToListAsync();

            if (!owners.Any())
            {
                return null;
            }

            var openStatuses = new[] { "New", "Contacted", "Qualified" };
            var counts = await _db.EnterpriseLeads
                .AsNoTracking()
                .Where(x => x.OwnerUserId != null && openStatuses.Contains(x.Status))
                .GroupBy(x => x.OwnerUserId!.Value)
                .Select(g => new { OwnerUserId = g.Key, Count = g.Count() })
                .ToListAsync();

            var best = owners
                .Select(o => new
                {
                    o.UserId,
                    o.Name,
                    Count = counts.FirstOrDefault(c => c.OwnerUserId == o.UserId)?.Count ?? 0
                })
                .OrderBy(x => x.Count)
                .ThenBy(x => x.UserId)
                .First();

            return new User { UserId = best.UserId, Name = best.Name };
        }

        public async Task<ReferralSummaryDto> GetOrCreateReferralCodeAsync(int userId)
        {
            var existing = await _db.ReferralCodes
                .Include(x => x.Usages)
                .FirstOrDefaultAsync(x => x.OwnerUserId == userId);

            if (existing == null)
            {
                var baseCode = await BuildUniqueCode(userId);
                existing = new ReferralCode
                {
                    OwnerUserId = userId,
                    Code = baseCode,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ReferralCodes.Add(existing);
                await _db.SaveChangesAsync();
            }

            return new ReferralSummaryDto
            {
                Code = existing.Code,
                IsActive = existing.IsActive,
                TotalReferrals = existing.Usages.Count
            };
        }

        public async Task<IReadOnlyList<ReferralUsageDto>> GetReferralUsageAsync(int userId, int take = 25)
        {
            take = Math.Clamp(take, 1, 100);

            var code = await _db.ReferralCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == userId);
            if (code == null)
            {
                return Array.Empty<ReferralUsageDto>();
            }

            var rows = await _db.ReferralUsages
                .AsNoTracking()
                .Where(x => x.ReferralCodeId == code.ReferralCodeId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(x => new ReferralUsageDto
                {
                    ReferredUserId = x.ReferredUserId,
                    ReferredUserName = x.ReferredUser.Name,
                    ReferredEmail = x.ReferredUser.Email,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            return rows;
        }

        public async Task ApplyReferralCodeAsync(int referredUserId, string referralCode)
        {
            var code = (referralCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            var refCode = await _db.ReferralCodes
                .FirstOrDefaultAsync(x => x.Code == code && x.IsActive);

            if (refCode == null)
            {
                throw new InvalidOperationException("Referral code is invalid.");
            }

            if (refCode.OwnerUserId == referredUserId)
            {
                throw new InvalidOperationException("You cannot use your own referral code.");
            }

            var already = await _db.ReferralUsages.AnyAsync(x => x.ReferredUserId == referredUserId);
            if (already)
            {
                return;
            }

            _db.ReferralUsages.Add(new ReferralUsage
            {
                ReferralCodeId = refCode.ReferralCodeId,
                ReferredUserId = referredUserId,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }

        private async Task<string> BuildUniqueCode(int userId)
        {
            var seed = userId.ToString("D4");
            for (var i = 0; i < 20; i++)
            {
                var code = $"IM{seed}{Random.Shared.Next(100, 999)}";
                var exists = await _db.ReferralCodes.AsNoTracking().AnyAsync(x => x.Code == code);
                if (!exists)
                {
                    return code;
                }
            }

            return $"IM{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }
    }
}
