using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace InfluencerMatch.API.Middleware
{
    public class AdminAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public AdminAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Admin authentication required." });
                return;
            }

            var role = context.User.FindFirstValue(ClaimTypes.Role);
            var tokenUse = context.User.FindFirstValue("token_use");
            if (!string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(tokenUse, "access", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Admin access denied." });
                return;
            }

            await _next(context);
        }
    }
}
