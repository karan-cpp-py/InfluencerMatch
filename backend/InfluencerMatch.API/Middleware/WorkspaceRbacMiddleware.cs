using System;
using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace InfluencerMatch.API.Middleware
{
    public class WorkspaceRbacMiddleware
    {
        private readonly RequestDelegate _next;

        public WorkspaceRbacMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            if (!context.Request.Path.StartsWithSegments("/api/workspace", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // These endpoints are allowed without existing workspace membership.
            if (IsMembershipOptionalEndpoint(context))
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

            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Invalid user context." });
                return;
            }

            var membership = await db.WorkspaceMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (membership == null)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "You are not part of any workspace." });
                return;
            }

            if (NeedsManagerPermission(context.Request)
                && !string.Equals(membership.Role, "Owner", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(membership.Role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Owner or Manager role is required for this workspace action." });
                return;
            }

            await _next(context);
        }

        private static bool IsMembershipOptionalEndpoint(HttpContext context)
        {
            var path = context.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
            var method = context.Request.Method;

            if (string.Equals(path, "/api/workspace", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (string.Equals(path, "/api/workspace/my-invites", StringComparison.OrdinalIgnoreCase)
                && string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(path, "/api/workspace/invites/accept", StringComparison.OrdinalIgnoreCase)
                && string.Equals(method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool NeedsManagerPermission(HttpRequest request)
        {
            var path = request.Path.Value ?? string.Empty;
            var method = request.Method;

            var inviteMutation = path.Contains("/invites", StringComparison.OrdinalIgnoreCase)
                && string.Equals(method, HttpMethods.Post, StringComparison.OrdinalIgnoreCase)
                && !path.EndsWith("/invites/accept", StringComparison.OrdinalIgnoreCase);

            var memberMutation = path.Contains("/members", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(method, HttpMethods.Patch, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(method, HttpMethods.Delete, StringComparison.OrdinalIgnoreCase));

            return inviteMutation || memberMutation;
        }
    }
}
