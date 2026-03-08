using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using InfluencerMatch.API.Configuration;
using Microsoft.AspNetCore.Http;
using InfluencerMatch.API.HealthChecks;
using AutoMapper;
using InfluencerMatch.API.Middleware;
using InfluencerMatch.Application.Interfaces;
using InfluencerMatch.Application.Services;
using InfluencerMatch.Infrastructure.Services.PaymentProviders;
using InfluencerMatch.Infrastructure.Services;
using InfluencerMatch.Infrastructure.Data;
using InfluencerMatch.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// configuration
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplicationInsightsTelemetry();

// YouTube Creator Discovery — legacy auto-discovery removed; creators are now
// added only when they register via CreatorChannelService.LinkChannelAsync.
// AddHostedService<CreatorDiscoveryWorker>() — permanently disabled
builder.Services.AddHttpClient();

// Creator Analytics
builder.Services.AddScoped<ICreatorRepository, CreatorRepository>();
builder.Services.AddScoped<ICreatorAnalyticsService, CreatorAnalyticsService>();
// AddHostedService<CreatorAnalyticsWorker>() — disabled: triggered manually by SuperAdmin

// Marketing Intelligence (brand detection + creator scoring)
builder.Services.AddScoped<IBrandPromotionService, InfluencerMatch.Infrastructure.Services.BrandPromotionService>();
builder.Services.AddScoped<ICreatorScoringService, InfluencerMatch.Infrastructure.Services.CreatorScoringService>();
// AddHostedService<MarketingIntelligenceWorker>() — disabled: triggered manually by SuperAdmin

// Innovative Features (Rising Creators, Brand Opportunities, Campaign Prediction, Price Estimation)
builder.Services.AddScoped<IRisingCreatorService,      InfluencerMatch.Infrastructure.Services.RisingCreatorService>();
builder.Services.AddScoped<IBrandOpportunityService,   InfluencerMatch.Infrastructure.Services.BrandOpportunityService>();
builder.Services.AddScoped<ICampaignPredictionService, InfluencerMatch.Infrastructure.Services.CampaignPredictionService>();
builder.Services.AddScoped<ICreatorPricingService,     InfluencerMatch.Infrastructure.Services.CampaignPredictionService>();
// AddHostedService<RisingCreatorWorker>() — disabled: triggered manually by SuperAdmin

// Feature 5: Viral Content Prediction
builder.Services.AddScoped<IViralContentService, InfluencerMatch.Infrastructure.Services.ViralContentService>();
// AddHostedService<ViralContentWorker>() — disabled: triggered manually by SuperAdmin

// Feature 6: Language Detection
builder.Services.AddScoped<ILanguageDetectionService, InfluencerMatch.Infrastructure.Services.LanguageDetectionService>();
// AddHostedService<LanguageDetectionWorker>() — disabled: triggered manually by SuperAdmin

// YouTube API quota tracker — singleton so daily usage persists across scoped service instances
builder.Services.AddSingleton<IYouTubeQuotaTracker, InfluencerMatch.Infrastructure.Services.YouTubeQuotaTracker>();

// Legacy influencer channel cache — holds live YouTube snapshots fetched by SuperAdmin job
builder.Services.AddSingleton<InfluencerMatch.API.Services.LegacyChannelCache>();

// Feature 7: Creator Registration + Channel Linking
builder.Services.AddScoped<ICreatorRegistrationService, InfluencerMatch.Infrastructure.Services.CreatorRegistrationService>();
builder.Services.AddScoped<ICreatorChannelService,      InfluencerMatch.Infrastructure.Services.CreatorChannelService>();
builder.Services.AddScoped<ICollaborationService,       InfluencerMatch.Infrastructure.Services.CollaborationService>();
builder.Services.AddScoped<INotificationService,        InfluencerMatch.Infrastructure.Services.NotificationService>();
// Feature 8: Video Analytics + Brand Collaboration Detection
builder.Services.AddScoped<IVideoAnalyticsService,      InfluencerMatch.Infrastructure.Services.VideoAnalyticsService>();
// AddHostedService<CreatorStatsUpdateWorker>() — disabled: triggered manually by SuperAdmin
// AddHostedService<VideoMetricsUpdateWorker>() — disabled: triggered manually by SuperAdmin

// CORS
builder.Services.AddCors(options =>
{
    var allowedOrigins = new List<string>();

    var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"];
    if (!string.IsNullOrWhiteSpace(configuredOrigins))
    {
        allowedOrigins.AddRange(
            configuredOrigins
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        );
    }

    var frontendBaseUrl = builder.Configuration["App:FrontendBaseUrl"];
    if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
    {
        allowedOrigins.Add(frontendBaseUrl);
    }

    // Local development defaults.
    allowedOrigins.Add("http://localhost:3000");
    allowedOrigins.Add("http://localhost:5173");

    allowedOrigins = allowedOrigins
        .Where(o => !string.IsNullOrWhiteSpace(o))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    options.AddPolicy("AllowVueApp", policy =>
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .WithOrigins(allowedOrigins.ToArray())
              .AllowCredentials());
});

// DbContext – using PostgreSQL via Npgsql
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenRepository, TokenRepository>();
builder.Services.AddScoped<IInfluencerRepository, InfluencerRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInfluencerService, InfluencerService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ISubscriptionAccessService, SubscriptionAccessService>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IGtmService, GtmService>();
builder.Services.AddScoped<IPaymentGatewayService, PaymentGatewayService>();
builder.Services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IPaymentProviderClient, StripePaymentProviderClient>();
builder.Services.AddScoped<IPaymentProviderClient, RazorpayPaymentProviderClient>();
builder.Services.AddScoped<IPaymentProviderClient, PayPalPaymentProviderClient>();
builder.Services.Configure<PaymentProviderOptions>(builder.Configuration.GetSection("PaymentProviders"));
builder.Services.Configure<PlatformStrategyOptions>(builder.Configuration.GetSection("PlatformStrategy"));
builder.Services.Configure<EmailNotificationOptions>(builder.Configuration.GetSection("EmailNotifications"));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("authPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("webhookPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("paymentsPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "live" })
    .AddCheck<PaymentProvidersHealthCheck>(
        name: "payment_providers",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "ready" });

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

var app = builder.Build();

// middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<CorrelationLoggingMiddleware>();

app.UseCors("AllowVueApp");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<BrandActivationGateMiddleware>();
app.UseMiddleware<AdminAccessMiddleware>();
app.UseMiddleware<SubscriptionAccessMiddleware>();
app.UseMiddleware<WorkspaceRbacMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var planService = scope.ServiceProvider.GetRequiredService<ISubscriptionPlanService>();
    await planService.SeedDefaultPlansAsync();
}

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live") || check.Tags.Contains("ready")
});

app.MapHealthChecks("/readiness", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Render/other PaaS expose the listen port via PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.Run();
