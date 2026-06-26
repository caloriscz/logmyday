using System.Diagnostics;

namespace LogMyDay.App.Extensions;

internal static class MiddlewareExtensions
{
    private const long SlowRequestThresholdMs = 1000;

    internal static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var startTimestamp = Stopwatch.GetTimestamp();

            await next();

            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            var statusCode = context.Response.StatusCode;

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
