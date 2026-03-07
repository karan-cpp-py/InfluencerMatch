using System;
using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InfluencerMatch.API.Middleware
{
    public class SubscriptionAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public SubscriptionAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISubscriptionAccessService accessService)
        {
            var pathRule = ResolvePathRule(context.Request.Path);
            if (pathRule == null)
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Authentication required." });
                return;
            }

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid user context." });
                return;
            }

            if (pathRule == "creator_search")
            {
                var access = await accessService.ValidateCreatorSearchAccessAsync(userId);
                if (!access.Allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = access.Reason,
                        remainingSearches = access.RemainingSearches,
                        requiredPlan = access.RequiredPlan,
                        currentPlan = access.CurrentPlan,
                        errorCode = access.ErrorCode,
                        upgradePath = "/plans"
                    });
                    return;
                }

                await _next(context);

                if (context.Response.StatusCode < 400)
                {
                    await accessService.RecordCreatorSearchUsageAsync(userId);
                }

                return;
            }

            var featureAccess = await accessService.ValidateFeatureAccessAsync(userId, pathRule);
            if (!featureAccess.Allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = featureAccess.Reason,
                    requiredPlan = featureAccess.RequiredPlan,
                    currentPlan = featureAccess.CurrentPlan,
                    errorCode = featureAccess.ErrorCode,
                    upgradePath = "/plans"
                });
                return;
            }

            await _next(context);
        }

        private static string? ResolvePathRule(PathString path)
        {
            var normalized = path.Value?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            if (normalized.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("/api/payments", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("/api/subscriptions", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (normalized.Equals("/api/creators/search", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("/api/marketplace/creators", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("/api/discovery/creators", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("/api/internal/creators", StringComparison.OrdinalIgnoreCase))
            {
                return "creator_search";
            }

            if ((normalized.StartsWith("/api/brands/", StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith("/api/creators/", StringComparison.OrdinalIgnoreCase))
                && (normalized.Contains("/analytics", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/video-analytics", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/creators/leaderboard", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/creators/compare", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/brand/opportunities", StringComparison.OrdinalIgnoreCase)))
            {
                return "advanced_analytics";
            }

            return null;
        }
    }
}
