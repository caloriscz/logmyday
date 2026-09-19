using LogMyDay.Api.Authentication;
using LogMyDay.Mcp;
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

                    // Only our own token shape. Any other bearer value falls through to the cookie
                    // scheme and fails there, exactly as it did before keys existed.
                    if (authHeader?.StartsWith(ApiKeyAuthDefaults.BearerTokenPrefix, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return ApiKeyAuthDefaults.SchemeName;
                    }

                    // The MCP endpoint is API-key only: never consult the browser cookie there, and
                    // challenge with Bearer instead of redirecting an agent to the login page.
                    if (context.Request.Path.StartsWithSegments(McpSchemaVersion.EndpointPath))
                    {
                        return ApiKeyAuthDefaults.SchemeName;
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
            .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("basic", _ => { })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(ApiKeyAuthDefaults.SchemeName, _ => { });

        services.AddSingleton<LogMyDay.Api.Authentication.AuthAttemptTracker>();
        services.AddSingleton<LogMyDay.Api.Authentication.PasswordVerificationCache>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim("is_admin", "true"));

            // MCP policies. All three insist the request was authenticated by an API key, so a
            // browser session can never drive the MCP endpoint (see McpPolicies for why).
            options.AddPolicy(McpPolicies.Read, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(ApiKeyAuthDefaults.AuthSourceClaim, ApiKeyAuthDefaults.AuthSourceValue));

            options.AddPolicy(McpPolicies.Write, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(ApiKeyAuthDefaults.AuthSourceClaim, ApiKeyAuthDefaults.AuthSourceValue)
                      .RequireClaim(ApiKeyAuthDefaults.ScopeClaim, ApiKeyAuthDefaults.ScopeWrite));

            options.AddPolicy(McpPolicies.Admin, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(ApiKeyAuthDefaults.AuthSourceClaim, ApiKeyAuthDefaults.AuthSourceValue)
                      .RequireClaim(ApiKeyAuthDefaults.ScopeClaim, ApiKeyAuthDefaults.ScopeWrite)
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

        services.AddScoped<CircuitRequestOrigin>();
        services.AddScoped<CookieAuthenticationHandler>();

        return services;
    }
}
