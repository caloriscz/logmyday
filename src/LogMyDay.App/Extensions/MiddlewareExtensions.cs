using System.Diagnostics;
using System.Globalization;
using LogMyDay.Api.Authentication;

namespace LogMyDay.App.Extensions;

internal static class MiddlewareExtensions
{
    private const long SlowRequestThresholdMs = 1000;

    internal static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var startTimestamp = Stopwatch.GetTimestamp();

            // Server-Timing has to be attached before the response starts, but the numbers only
            // exist after next() returns. OnStarting runs late enough to have the auth timings
            // (authentication happens inside next()) and early enough to still set headers.
            context.Response.OnStarting(() =>
            {
                var serverTiming = BuildServerTiming(context, startTimestamp);
                if (serverTiming != null)
                {
                    context.Response.Headers["Server-Timing"] = serverTiming;
                }

                return Task.CompletedTask;
            });

            await next();

            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            var statusCode = context.Response.StatusCode;

            // Basic auth re-derives the password on every request; the cookie scheme does not.
            // Surfacing the verify cost separately is what distinguishes a slow query from a
            // slow credential check when comparing the mobile and web clients.
            if (context.Items[BasicAuthHandler.VerifyMsItemKey] is double verifyMs)
            {
                var lookupMs = context.Items[BasicAuthHandler.LookupMsItemKey] as double? ?? 0;
                logger.LogInformation(
                    "[perf] {Method} {Path} {StatusCode} total={ElapsedMs:0.0}ms argon2={VerifyMs:0.0}ms cached={Cached} lookup={LookupMs:0.0}ms scheme=basic params={Params}",
                    context.Request.Method, context.Request.Path, statusCode, elapsedMs, verifyMs,
                    context.Items[BasicAuthHandler.CacheHitItemKey] as bool? ?? false, lookupMs,
                    context.Items[BasicAuthHandler.ParamsItemKey] as string ?? "unknown");
            }

            // Normal traffic stays at Debug to keep the hot path quiet; errors and slow
            // requests are elevated so they remain observable at the default Information level.
            var level = statusCode >= 500 ? LogLevel.Error
                : statusCode >= 400 || elapsedMs >= SlowRequestThresholdMs ? LogLevel.Warning
                : LogLevel.Debug;

            // Path only — never the query string, which can carry PII.
            logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0} ms",
                context.Request.Method, context.Request.Path, statusCode, elapsedMs);
        });

        return app;
    }

    /// <summary>
    /// "auth;dur=..., total;dur=..." for the client to attribute its own latency: anything the
    /// client measures beyond `total` is connection setup plus transfer. Only emitted for Basic
    /// auth requests, which is the mobile client — cookie requests have nothing interesting here.
    /// </summary>
    private static string? BuildServerTiming(HttpContext context, long startTimestamp)
    {
        if (context.Items[BasicAuthHandler.VerifyMsItemKey] is not double verifyMs)
        {
            return null;
        }

        var lookupMs = context.Items[BasicAuthHandler.LookupMsItemKey] as double? ?? 0;
        var totalMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

        return string.Create(CultureInfo.InvariantCulture,
            $"argon2;dur={verifyMs:0.0}, lookup;dur={lookupMs:0.0}, total;dur={totalMs:0.0}");
    }

    internal static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XXSSProtection = "1; mode=block";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            await next();
        });

        return app;
    }
}
