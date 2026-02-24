using Microsoft.AspNetCore.RateLimiting;

namespace LogMyDay.App.Extensions;

internal static class RateLimitingExtensions
{
    internal static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddSlidingWindowLimiter("api", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 100;
                opt.SegmentsPerWindow = 6;
            });

            options.AddSlidingWindowLimiter("auth", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(15);
                opt.PermitLimit = 10;
                opt.SegmentsPerWindow = 3;
            });

            options.AddSlidingWindowLimiter("ai", opt =>
            {
                opt.Window = TimeSpan.FromMinutes(1);
                opt.PermitLimit = 20;
                opt.SegmentsPerWindow = 4;
            });

            options.RejectionStatusCode = 429;
        });

        return services;
    }
}
