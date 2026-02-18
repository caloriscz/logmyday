namespace LogMyDay.App.Extensions;

internal static class MiddlewareExtensions
{
    internal static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("========== HTTP REQUEST ==========");
            logger.LogInformation("= Method: {Method}", context.Request.Method);
            logger.LogInformation("= Path: {Path}", context.Request.Path);
            logger.LogInformation("= QueryString: {QueryString}", context.Request.QueryString);

            var sensitiveHeaders = new[] { "Authorization", "Cookie", "X-CSRF-Token", "Set-Cookie" };
            var safeHeaders = context.Request.Headers
                .Where(h => !sensitiveHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                .Select(h => $"{h.Key}={string.Join(",", h.Value.ToArray())}");
            logger.LogInformation("= Headers: {Headers}", string.Join(", ", safeHeaders));

            if (context.Request.HasFormContentType && context.Request.Method == "POST")
            {
                logger.LogInformation("= Form Data: [REDACTED - contains sensitive information]");
            }

            logger.LogInformation("= User Authenticated: {IsAuthenticated}", context.User?.Identity?.IsAuthenticated);
            logger.LogInformation("= User Name: {UserName}", context.User?.Identity?.Name ?? "null");

            await next();

            logger.LogInformation("= Response Status: {StatusCode}", context.Response.StatusCode);

            var safeResponseHeaders = context.Response.Headers
                .Where(h => !sensitiveHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                .Select(h => $"{h.Key}={string.Join(",", h.Value.ToArray())}");
            logger.LogInformation("= Response Headers: {ResponseHeaders}", string.Join(", ", safeResponseHeaders));
            logger.LogInformation("========== HTTP REQUEST END ==========");
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
