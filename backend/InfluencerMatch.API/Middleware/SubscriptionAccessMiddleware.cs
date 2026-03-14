using System;
using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace InfluencerMatch.API.Middleware
{
    public class SubscriptionAccessMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _env;

        public SubscriptionAccessMiddleware(RequestDelegate next, IHostEnvironment env)
        {
            _next = next;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context, ISubscriptionAccessService accessService)
        {
            // Subscription gates disabled — all authenticated users have unrestricted access.
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
