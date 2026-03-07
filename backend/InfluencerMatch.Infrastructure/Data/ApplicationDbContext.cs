using InfluencerMatch.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Influencer> Influencers { get; set; }
        public DbSet<Campaign> Campaigns { get; set; }
        public DbSet<MatchResult> MatchResults { get; set; }
        public DbSet<Creator> Creators { get; set; }
        public DbSet<CreatorAnalytics> CreatorAnalytics { get; set; }
        public DbSet<CreatorGrowth> CreatorGrowth { get; set; }
        public DbSet<BrandMention> BrandMentions { get; set; }
        public DbSet<CreatorScore> CreatorScores { get; set; }
        public DbSet<CreatorGrowthScore> CreatorGrowthScores { get; set; }
        public DbSet<Video> Videos { get; set; }
        public DbSet<VideoViralScore> VideoViralScores { get; set; }

        // Feature 7: Creator Registration + Channel Linking
        public DbSet<CreatorProfile>       CreatorProfiles       { get; set; }
        public DbSet<CreatorChannel>       CreatorChannels       { get; set; }
        public DbSet<ChannelVideo>         ChannelVideos         { get; set; }
        public DbSet<VideoMetrics>         VideoMetrics          { get; set; }
        public DbSet<CollaborationRequest> CollaborationRequests { get; set; }

        // Feature 8: Video Analytics + Brand Collaboration Detection
        public DbSet<VideoAnalytics> VideoAnalytics { get; set; }

        // Subscription and billing
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<PaymentRecord> PaymentRecords { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<WebhookEvent> WebhookEvents { get; set; }
        public DbSet<BrandWaitlistEntry> BrandWaitlistEntries { get; set; }
        public DbSet<CollaborationMilestone> CollaborationMilestones { get; set; }
        public DbSet<CollaborationActivity> CollaborationActivities { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<FunnelEvent> FunnelEvents { get; set; }
        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<WorkspaceMember> WorkspaceMembers { get; set; }
        public DbSet<WorkspaceInvite> WorkspaceInvites { get; set; }
        public DbSet<WorkspaceAuditLog> WorkspaceAuditLogs { get; set; }
        public DbSet<EnterpriseLead> EnterpriseLeads { get; set; }
        public DbSet<EnterpriseLeadActivity> EnterpriseLeadActivities { get; set; }
        public DbSet<ReferralCode> ReferralCodes { get; set; }
        public DbSet<ReferralUsage> ReferralUsages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.CustomerType);

            // one-to-one between User and Influencer
            modelBuilder.Entity<User>()
                .HasOne(u => u.InfluencerProfile)
                .WithOne(i => i.User)
                .HasForeignKey<Influencer>(i => i.UserId);

            modelBuilder.Entity<Campaign>()
                .HasOne(c => c.User)
                .WithMany(u => u.Campaigns)
                .HasForeignKey(c => c.BrandId);

            modelBuilder.Entity<MatchResult>()
                .HasOne(m => m.Campaign)
                .WithMany(c => c.MatchResults)
                .HasForeignKey(m => m.CampaignId);

            modelBuilder.Entity<MatchResult>()
                .HasOne(m => m.Influencer)
                .WithMany(i => i.MatchResults)
                .HasForeignKey(m => m.InfluencerId);

            modelBuilder.Entity<Creator>()
                .HasIndex(c => c.ChannelId)
                .IsUnique();

            // Creator → User FK (every Creator must be a registered user)
            modelBuilder.Entity<Creator>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Creator>()
                .HasIndex(c => c.UserId);

            // Indexes for language-based filtering queries
            modelBuilder.Entity<Creator>()
                .HasIndex(c => c.Language);

            modelBuilder.Entity<Creator>()
                .HasIndex(c => c.Region);

            // Indexes for tier-based and subscriber-range queries
            modelBuilder.Entity<Creator>()
                .HasIndex(c => c.Subscribers);

            modelBuilder.Entity<Creator>()
                .HasIndex(c => c.CreatorTier);

            modelBuilder.Entity<Creator>()
                .HasIndex(c => c.IsSmallCreator);

            // Explicit PK declarations for analytics tables
            modelBuilder.Entity<CreatorAnalytics>()
                .HasKey(a => a.CreatorAnalyticsId);

            modelBuilder.Entity<CreatorGrowth>()
                .HasKey(g => g.GrowthId);

            // Ensure only one analytics row per creator (latest wins via upsert)
            modelBuilder.Entity<CreatorAnalytics>()
                .HasIndex(a => a.CreatorId)
                .IsUnique();

            modelBuilder.Entity<CreatorAnalytics>()
                .HasOne(a => a.Creator)
                .WithMany()
                .HasForeignKey(a => a.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CreatorGrowth>()
                .HasOne(g => g.Creator)
                .WithMany()
                .HasForeignKey(g => g.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CreatorGrowth>()
                .HasIndex(g => new { g.CreatorId, g.RecordedAt });

            // ── BrandMentions ───────────────────────────────────────────────
            modelBuilder.Entity<BrandMention>()
                .HasKey(m => m.BrandMentionId);

            modelBuilder.Entity<BrandMention>()
                .HasOne(m => m.Creator)
                .WithMany()
                .HasForeignKey(m => m.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique per detected brand per video (prevents duplicate scan entries)
            modelBuilder.Entity<BrandMention>()
                .HasIndex(m => new { m.VideoId, m.BrandName })
                .IsUnique();

            // Index for efficient brand-name queries
            modelBuilder.Entity<BrandMention>()
                .HasIndex(m => m.BrandName);

            modelBuilder.Entity<BrandMention>()
                .HasIndex(m => m.CreatorId);

            // ── CreatorScores ───────────────────────────────────────────────
            modelBuilder.Entity<CreatorScore>()
                .HasKey(s => s.ScoreId);

            modelBuilder.Entity<CreatorScore>()
                .HasOne(s => s.Creator)
                .WithMany()
                .HasForeignKey(s => s.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // One score row per creator (latest wins via upsert)
            modelBuilder.Entity<CreatorScore>()
                .HasIndex(s => s.CreatorId)
                .IsUnique();

            // Index for leaderboard queries sorted by score descending
            modelBuilder.Entity<CreatorScore>()
                .HasIndex(s => s.Score);

            // ── CreatorGrowthScores ─────────────────────────────────────────
            modelBuilder.Entity<CreatorGrowthScore>()
                .HasKey(g => g.CreatorGrowthScoreId);

            modelBuilder.Entity<CreatorGrowthScore>()
                .HasOne(g => g.Creator)
                .WithMany()
                .HasForeignKey(g => g.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // One growth-score row per creator (latest wins via upsert)
            modelBuilder.Entity<CreatorGrowthScore>()
                .HasIndex(g => g.CreatorId)
                .IsUnique();

            // Index for sorting by growth rate
            modelBuilder.Entity<CreatorGrowthScore>()
                .HasIndex(g => g.GrowthRate);

            // ── Videos ─────────────────────────────────────────────────────
            modelBuilder.Entity<Video>()
                .HasKey(v => v.Id);

            modelBuilder.Entity<Video>()
                .HasIndex(v => v.VideoId)
                .IsUnique();

            modelBuilder.Entity<Video>()
                .HasIndex(v => v.PublishedAt);

            modelBuilder.Entity<Video>()
                .HasOne(v => v.Creator)
                .WithMany()
                .HasForeignKey(v => v.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── VideoViralScores ────────────────────────────────────────────
            modelBuilder.Entity<VideoViralScore>()
                .HasKey(s => s.ScoreId);

            // One score row per video (latest wins via upsert)
            modelBuilder.Entity<VideoViralScore>()
                .HasIndex(s => s.VideoId)
                .IsUnique();

            // Index for trending queries sorted by ViralScore descending
            modelBuilder.Entity<VideoViralScore>()
                .HasIndex(s => s.ViralScore);

            modelBuilder.Entity<VideoViralScore>()
                .HasOne(s => s.Video)
                .WithMany()
                .HasForeignKey(s => s.VideoId)
                .HasPrincipalKey(v => v.VideoId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Feature 7: Creator Registration + Channel Linking ─────────────

            // CreatorProfile — one per User
            modelBuilder.Entity<CreatorProfile>()
                .HasIndex(p => p.UserId)
                .IsUnique();
            modelBuilder.Entity<CreatorProfile>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // CreatorChannel — unique YouTube channel ID
            modelBuilder.Entity<CreatorChannel>()
                .HasIndex(c => c.ChannelId)
                .IsUnique();
            modelBuilder.Entity<CreatorChannel>()
                .HasOne(c => c.CreatorProfile)
                .WithMany(p => p.Channels)
                .HasForeignKey(c => c.CreatorProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CreatorChannel>()
                .HasIndex(c => c.Subscribers);
            modelBuilder.Entity<CreatorChannel>()
                .HasIndex(c => c.EngagementRate);
            modelBuilder.Entity<CreatorChannel>()
                .HasIndex(c => c.CreatorTier);

            // ChannelVideo — string FK via principal key on CreatorChannel.ChannelId
            modelBuilder.Entity<ChannelVideo>()
                .HasIndex(v => v.YoutubeVideoId)
                .IsUnique();
            modelBuilder.Entity<ChannelVideo>()
                .HasIndex(v => v.PublishedAt);
            modelBuilder.Entity<ChannelVideo>()
                .HasOne(v => v.Channel)
                .WithMany(c => c.Videos)
                .HasForeignKey(v => v.ChannelId)
                .HasPrincipalKey(c => c.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            // VideoMetrics — string FK via principal key on ChannelVideo.YoutubeVideoId
            modelBuilder.Entity<VideoMetrics>()
                .HasKey(m => m.MetricId);
            modelBuilder.Entity<VideoMetrics>()
                .HasIndex(m => new { m.YoutubeVideoId, m.RecordedAt });
            modelBuilder.Entity<VideoMetrics>()
                .HasOne(m => m.Video)
                .WithMany(v => v.Metrics)
                .HasForeignKey(m => m.YoutubeVideoId)
                .HasPrincipalKey(v => v.YoutubeVideoId)
                .OnDelete(DeleteBehavior.Cascade);

            // CollaborationRequest
            modelBuilder.Entity<CollaborationRequest>()
                .HasKey(r => r.RequestId);
            modelBuilder.Entity<CollaborationRequest>()
                .HasIndex(r => r.BrandUserId);
            modelBuilder.Entity<CollaborationRequest>()
                .HasIndex(r => r.CreatorProfileId);
            modelBuilder.Entity<CollaborationRequest>()
                .HasOne(r => r.Brand)
                .WithMany()
                .HasForeignKey(r => r.BrandUserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CollaborationRequest>()
                .HasOne(r => r.Creator)
                .WithMany(p => p.CollaborationRequests)
                .HasForeignKey(r => r.CreatorProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CollaborationMilestone>()
                .HasKey(m => m.CollaborationMilestoneId);
            modelBuilder.Entity<CollaborationMilestone>()
                .HasIndex(m => new { m.RequestId, m.Status });
            modelBuilder.Entity<CollaborationMilestone>()
                .HasOne(m => m.Request)
                .WithMany()
                .HasForeignKey(m => m.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CollaborationActivity>()
                .HasKey(a => a.CollaborationActivityId);
            modelBuilder.Entity<CollaborationActivity>()
                .HasIndex(a => new { a.RequestId, a.CreatedAt });
            modelBuilder.Entity<CollaborationActivity>()
                .HasOne(a => a.Request)
                .WithMany()
                .HasForeignKey(a => a.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Feature 8: VideoAnalytics ─────────────────────────────────
            modelBuilder.Entity<VideoAnalytics>()
                .HasKey(v => v.VideoAnalyticsId);

            // One row per YouTube video (upsert on re-scan)
            modelBuilder.Entity<VideoAnalytics>()
                .HasIndex(v => v.YoutubeVideoId)
                .IsUnique();

            modelBuilder.Entity<VideoAnalytics>()
                .HasIndex(v => v.CreatorId);

            modelBuilder.Entity<VideoAnalytics>()
                .HasIndex(v => v.BrandName);

            modelBuilder.Entity<VideoAnalytics>()
                .HasIndex(v => v.VideoType);

            // Brand waitlist for deferred activation phase.
            modelBuilder.Entity<BrandWaitlistEntry>()
                .HasIndex(x => x.Email);

            modelBuilder.Entity<BrandWaitlistEntry>()
                .HasIndex(x => new { x.Email, x.CompanyName })
                .IsUnique();

            modelBuilder.Entity<BrandWaitlistEntry>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<VideoAnalytics>()
                .HasOne(v => v.Creator)
                .WithMany()
                .HasForeignKey(v => v.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Subscriptions + Billing ─────────────────────────────────
            modelBuilder.Entity<SubscriptionPlan>()
                .HasIndex(p => p.PlanName)
                .IsUnique();

            modelBuilder.Entity<UserSubscription>()
                .HasOne(s => s.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSubscription>()
                .HasOne(s => s.Plan)
                .WithMany(p => p.UserSubscriptions)
                .HasForeignKey(s => s.PlanId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserSubscription>()
                .HasIndex(s => new { s.UserId, s.Status });

            modelBuilder.Entity<UserSubscription>()
                .HasIndex(s => s.EndDate);

            modelBuilder.Entity<PaymentRecord>()
                .HasOne(p => p.Subscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PaymentRecord>()
                .HasIndex(p => p.ProviderPaymentId);

            modelBuilder.Entity<PaymentRecord>()
                .HasIndex(p => p.Provider);

            modelBuilder.Entity<PaymentRecord>()
                .HasIndex(p => p.ProviderEventId);

            // ── Auth + idempotency + invoices + webhook replay protection ──
            modelBuilder.Entity<RefreshToken>()
                .HasOne(r => r.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(r => r.TokenHash)
                .IsUnique();

            modelBuilder.Entity<RefreshToken>()
                .HasIndex(r => new { r.UserId, r.TokenFamily });

            modelBuilder.Entity<IdempotencyRecord>()
                .HasOne(i => i.User)
                .WithMany(u => u.IdempotencyRecords)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IdempotencyRecord>()
                .HasIndex(i => new { i.UserId, i.Scope, i.IdempotencyKey })
                .IsUnique();

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.User)
                .WithMany(u => u.Invoices)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Subscription)
                .WithMany()
                .HasForeignKey(i => i.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.ProviderInvoiceId);

            modelBuilder.Entity<WebhookEvent>()
                .HasIndex(e => new { e.Provider, e.EventId })
                .IsUnique();

            modelBuilder.Entity<WebhookEvent>()
                .HasIndex(e => e.CreatedAt);

            modelBuilder.Entity<UserNotification>()
                .HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
            modelBuilder.Entity<UserNotification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FunnelEvent>()
                .HasIndex(f => new { f.EventName, f.CreatedAt });
            modelBuilder.Entity<FunnelEvent>()
                .HasIndex(f => new { f.UserId, f.CreatedAt });

            modelBuilder.Entity<Workspace>()
                .HasIndex(x => x.OwnerUserId);
            modelBuilder.Entity<Workspace>()
                .HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkspaceMember>()
                .HasIndex(x => new { x.WorkspaceId, x.UserId })
                .IsUnique();
            modelBuilder.Entity<WorkspaceMember>()
                .HasIndex(x => new { x.WorkspaceId, x.Role });
            modelBuilder.Entity<WorkspaceMember>()
                .HasOne(x => x.Workspace)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<WorkspaceMember>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkspaceInvite>()
                .HasIndex(x => x.InviteToken)
                .IsUnique();
            modelBuilder.Entity<WorkspaceInvite>()
                .HasIndex(x => new { x.WorkspaceId, x.Email, x.Status });
            modelBuilder.Entity<WorkspaceInvite>()
                .HasOne(x => x.Workspace)
                .WithMany(x => x.Invites)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<WorkspaceInvite>()
                .HasOne(x => x.InvitedByUser)
                .WithMany()
                .HasForeignKey(x => x.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<WorkspaceInvite>()
                .HasOne(x => x.AcceptedByUser)
                .WithMany()
                .HasForeignKey(x => x.AcceptedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkspaceAuditLog>()
                .HasIndex(x => new { x.WorkspaceId, x.CreatedAt });
            modelBuilder.Entity<WorkspaceAuditLog>()
                .HasOne(x => x.Workspace)
                .WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<WorkspaceAuditLog>()
                .HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<EnterpriseLead>()
                .HasIndex(x => x.WorkEmail);
            modelBuilder.Entity<EnterpriseLead>()
                .HasIndex(x => x.CreatedAt);
            modelBuilder.Entity<EnterpriseLead>()
                .HasIndex(x => x.Status);
            modelBuilder.Entity<EnterpriseLead>()
                .HasIndex(x => x.OwnerUserId);
            modelBuilder.Entity<EnterpriseLead>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<EnterpriseLead>()
                .HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<EnterpriseLeadActivity>()
                .HasIndex(x => new { x.EnterpriseLeadId, x.CreatedAt });
            modelBuilder.Entity<EnterpriseLeadActivity>()
                .HasOne(x => x.EnterpriseLead)
                .WithMany(x => x.Activities)
                .HasForeignKey(x => x.EnterpriseLeadId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<EnterpriseLeadActivity>()
                .HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ReferralCode>()
                .HasIndex(x => x.Code)
                .IsUnique();
            modelBuilder.Entity<ReferralCode>()
                .HasIndex(x => x.OwnerUserId)
                .IsUnique();
            modelBuilder.Entity<ReferralCode>()
                .HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReferralUsage>()
                .HasIndex(x => new { x.ReferralCodeId, x.ReferredUserId })
                .IsUnique();
            modelBuilder.Entity<ReferralUsage>()
                .HasIndex(x => x.CreatedAt);
            modelBuilder.Entity<ReferralUsage>()
                .HasOne(x => x.ReferralCode)
                .WithMany(x => x.Usages)
                .HasForeignKey(x => x.ReferralCodeId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ReferralUsage>()
                .HasOne(x => x.ReferredUser)
                .WithMany()
                .HasForeignKey(x => x.ReferredUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}