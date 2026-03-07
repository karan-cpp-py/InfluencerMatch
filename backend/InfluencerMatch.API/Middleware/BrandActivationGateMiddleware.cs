using System;
using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.API.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace InfluencerMatch.API.Middleware
{
    public class BrandActivationGateMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly PlatformStrategyOptions _options;

        public BrandActivationGateMiddleware(RequestDelegate next, IOptions<PlatformStrategyOptions> options)
        {
            _next = next;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_options.BrandActivationEnabled)
            {
                await _next(context);
                return;
            }

            var path = context.Request.Path.Value ?? string.Empty;
            if (!IsBrandActivationPath(path))
            {
                await _next(context);
                return;
            }

            var role = context.User.FindFirstValue(ClaimTypes.Role);
            if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status423Locked;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Brand Activation is currently in private waitlist mode while creator intelligence network scales.",
                positioning = _options.PositioningLine,
                brandPilotInviteOnly = _options.BrandPilotInviteOnly
            });
        }

        private static bool IsBrandActivationPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            return path.StartsWith("/api/marketplace", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/campaign", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/brands", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/collaboration", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/match", StringComparison.OrdinalIgnoreCase);
        }
    }
}
