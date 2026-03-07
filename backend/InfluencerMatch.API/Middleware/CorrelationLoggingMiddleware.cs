using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace InfluencerMatch.API.Middleware
{
    public class CorrelationLoggingMiddleware
    {
        private const string CorrelationHeader = "X-Correlation-Id";
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationLoggingMiddleware> _logger;

        public CorrelationLoggingMiddleware(RequestDelegate next, ILogger<CorrelationLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(CorrelationHeader, out var fromHeader)
                ? fromHeader.ToString()
                : Guid.NewGuid().ToString("N");

            context.Items[CorrelationHeader] = correlationId;
            context.Response.Headers[CorrelationHeader] = correlationId;

            var sw = Stopwatch.StartNew();
            try
            {
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["correlationId"] = correlationId,
                    ["requestId"] = context.TraceIdentifier,
                    ["userId"] = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                    ["endpoint"] = context.Request.Path.ToString()
                }))
                {
                    await _next(context);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unhandled exception at endpoint={Endpoint}, requestId={RequestId}, correlationId={CorrelationId}",
                    context.Request.Path,
                    context.TraceIdentifier,
                    correlationId);
                throw;
            }
            finally
            {
                sw.Stop();
                _logger.LogInformation(
                    "Request completed status={StatusCode} method={Method} endpoint={Endpoint} durationMs={Duration}",
                    context.Response.StatusCode,
                    context.Request.Method,
                    context.Request.Path,
                    sw.ElapsedMilliseconds);
            }
        }
    }
}
