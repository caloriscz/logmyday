using LogMyDay.Api.Authentication;
using LogMyDay.App.Authentication;
using Microsoft.AspNetCore.Authentication;
using Serilog;

namespace LogMyDay.App.Extensions;

internal static class AuthenticationExtensions
{
    internal static IServiceCollection AddAppAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "smart-auth";
                options.DefaultChallengeScheme = "smart-auth";
            })
            .AddPolicyScheme("smart-auth", "Smart Authentication", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

                    if (authHeader?.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return "basic";
                    }

                    return "lmd-cookie";
                };
            })
            .AddCookie("lmd-cookie", options =>
            {
                options.Cookie.Name = "lmd.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.LoginPath = "/login";
                options.LogoutPath = "/api/auth/logout";
                options.AccessDeniedPath = "/access-denied";
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = 403;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnSigningIn = context =>
                {
                    Log.Information("Cookie authentication: User signing in - {Principal}", context.Principal?.Identity?.Name);
                    return Task.CompletedTask;
                };
                options.Events.OnSignedIn = context =>
                {
                    Log.Information("Cookie authentication: User signed in successfully - {Principal}", context.Principal?.Identity?.Name);
                    return Task.CompletedTask;
                };
                options.Events.OnSigningOut = context =>
                {
                    Log.Information("Cookie authentication: User signing out - {User}", context.HttpContext.User?.Identity?.Name);
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = context =>
                {
                    Log.Debug("Cookie authentication: Validating principal - {Principal}, IsAuthenticated: {IsAuthenticated}",
                        context.Principal?.Identity?.Name, context.Principal?.Identity?.IsAuthenticated);
                    return Task.CompletedTask;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("basic", _ => { });

        services.AddSingleton<LogMyDay.Api.Authentication.AuthAttemptTracker>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("is_admin", "true"));
        });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "lmd.csrf";
            options.Cookie.HttpOnly = false;
            options.HeaderName = "X-CSRF-Token";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        services.AddScoped<CookieAuthenticationHandler>();

        return services;
    }
}
