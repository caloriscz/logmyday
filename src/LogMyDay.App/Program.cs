using ApexCharts;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Application.Services.Ai;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Email;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Api.Security;
using LogMyDay.App.Authentication;
using LogMyDay.App.Components;
using LogMyDay.Shared.Interfaces;
using LogMyDay.App.Services;
using LogMyDay.App.Services.Charts;
using LogMyDay.Shared.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.OpenApi.Models;
using OpenAI;
using Refit;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// Load Docker secrets from /run/secrets if running in Docker environment
if (builder.Environment.EnvironmentName == "Docker")
{
    builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true, reloadOnChange: false);

    // Map file-based AI API key secret to configuration
    var aiApiKey = builder.Configuration["ai_api_key"];
    if (!string.IsNullOrWhiteSpace(aiApiKey))
    {
        builder.Configuration["AI:ApiKey"] = aiApiKey;
    }
}

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

var services = builder.Services;

var refitSerializerOptions = JsonSerializationSettings.CreateDefault();
var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(refitSerializerOptions)
};

// Build connection string with Docker secrets if in Docker environment
string connectionString;
if (builder.Environment.EnvironmentName == "Docker")
{
    var dbPassword = builder.Configuration["db_password"];
    var dbHost = builder.Configuration["DB_HOST"] ?? "host.docker.internal,1439";
    var dbName = builder.Configuration["DB_NAME"] ?? "logmyday";
    var dbUser = builder.Configuration["DB_USER"] ?? "sa";
    
    // Note: DB_HOST should include port if needed (e.g., "host.docker.internal,1439")
    connectionString = $"Server={dbHost};Database={dbName};User Id={dbUser};Password={dbPassword};Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

// Don't register DbContext in Test environment - let test factory handle it
if (builder.Environment.EnvironmentName != "Test")
{
    services.AddDbContext<LogMyDayDbContext>(options =>
    {
        options.UseSqlServer(connectionString);
    });
}

// Configure Data Protection to persist keys and survive app restarts/deployments
// This prevents users from being logged out after every deployment or app restart
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionPath); // Ensure directory exists

services.AddDataProtection()
    .SetApplicationName("LogMyDay") // Ensures keys work across app restarts
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath)) // Persist keys to disk
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90)); // Keys valid for 90 days

Log.Information("Data Protection configured: Keys will be persisted to {Path}", dataProtectionPath);

services.AddMemoryCache();
services.AddHttpContextAccessor();

// Add health checks
services.AddHealthChecks();

// Configure cookie authentication (default scheme) and basic auth for mobile
// Configure authentication with support for both cookie (Blazor Server) and Basic (Mobile API)
// Use a policy scheme that tries both authentication methods
services.AddAuthentication(options =>
    {
        // Use a composite scheme that tries both cookie and basic
        options.DefaultScheme = "smart-auth";
        options.DefaultChallengeScheme = "smart-auth";
    })
    .AddPolicyScheme("smart-auth", "Smart Authentication", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            // If the request has an Authorization header with "Basic", use Basic auth
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader?.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return "basic";
            }

            // Otherwise, use cookie auth
            return "lmd-cookie";
        };
    })
    .AddCookie("lmd-cookie", options =>
    {
        options.Cookie.Name = "lmd.auth";
        options.Cookie.HttpOnly = true;
        // Always require HTTPS for security - prevents session hijacking
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
                // For API requests, return 401 instead of redirect
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            }
            // For regular requests, redirect to login
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
    .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("basic", options => { });

// Add authentication attempt tracking service
services.AddSingleton<LogMyDay.Api.Authentication.AuthAttemptTracker>();

// Add authorization with admin policy
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_admin", "true"));
});

// Configure CSRF protection
services.AddAntiforgery(options =>
{
    options.Cookie.Name = "lmd.csrf";
    options.Cookie.HttpOnly = false; // readable by client for double-submit pattern
    options.HeaderName = "X-CSRF-Token";
    // Always require HTTPS for security - prevents token theft
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

services.AddRateLimiter(options =>
{
    // General API rate limiting using sliding window
    options.AddSlidingWindowLimiter("api", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100; // 100 requests per minute per IP
        opt.SegmentsPerWindow = 6; // 10-second segments
    });

    // Stricter authentication endpoint limiting
    options.AddSlidingWindowLimiter("auth", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(15);
        opt.PermitLimit = 10; // 10 auth attempts per 15 minutes per IP
        opt.SegmentsPerWindow = 3; // 5-minute segments
    });

    // AI endpoint rate limiting — more restrictive to control API costs
    options.AddSlidingWindowLimiter("ai", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 20; // 20 AI requests per minute per IP
        opt.SegmentsPerWindow = 4; // 15-second segments
    });

    options.RejectionStatusCode = 429; // Too Many Requests
});

services.AddRazorComponents().AddInteractiveServerComponents();

// Add ApexCharts for data visualization
services.AddApexCharts();

services.AddControllers()
    .AddJsonOptions(options =>
    {
        JsonSerializationSettings.Configure(options.JsonSerializerOptions);
    });
services.AddEndpointsApiExplorer();

services.AddScoped<IActivityService, ActivityService>();
services.AddScoped<ITagService, TagService>();
services.AddScoped<IUnitService, UnitService>();
services.AddScoped<ITagOptionListService, TagOptionListService>();
services.AddScoped<INotificationService, NotificationService>();
services.AddScoped<IBackupService, BackupService>();
services.AddScoped<IExcelExportService, ExportService>();
services.AddScoped<ISettingsService, SettingsService>();

// AI services (new factory-based approach with runtime reconfiguration support)
services.AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection(AiOptions.SectionName));

services.AddSingleton<IAiChatClientFactory, AiChatClientFactory>();
services.AddSingleton<IRouteDiscoveryService, RouteDiscoveryService>();
services.AddScoped<AiToolFunctions>();
services.AddScoped<IAiAssistantService, AiAssistantService>();

Log.Information("AI assistant services configured (availability determined at runtime)");

// Repository layer
services.AddScoped<IActivityRepository, ActivityRepository>();
services.AddScoped<ITagRepository, TagRepository>();
services.AddScoped<IUnitRepository, UnitRepository>();
services.AddScoped<IQuantityRepository, QuantityRepository>();

// UI services
services.AddScoped<IPageTitleService, PageTitleService>();
services.AddScoped<IUserPreferencesService, UserPreferencesService>();

// Chart services
services.AddScoped<IChartPreferencesService, ChartPreferencesService>();
services.AddScoped<IChartDataService, ChartDataService>();

// Authentication and user services
services.AddScoped<IPasswordHasher, Argon2IdPasswordHasher>();
services.AddScoped<IUserService, UserService>();
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .PostConfigure(options =>
    {
        options.PasswordResetUrl = ResolvePasswordResetUrl(options.PasswordResetUrl, builder.Configuration["Api:BaseAddress"]);

        Log.Information("Password reset URL configured to {PasswordResetUrl}", options.PasswordResetUrl);
    });
services.AddScoped<IEmailSender, MailKitEmailSender>();

// Keep existing credential store for Blazor Server (for backwards compatibility)
services.AddSingleton<CredentialStore>();

// Register the cookie authentication handler for forwarding cookies to API calls
services.AddScoped<CookieAuthenticationHandler>();

services.AddRefitClient<IActivityApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<CookieAuthenticationHandler>();

// Add new authentication API clients
services.AddRefitClient<IAuthApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<CookieAuthenticationHandler>();

services.AddRefitClient<IUsersApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<CookieAuthenticationHandler>();

services.AddRefitClient<IAccountApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<CookieAuthenticationHandler>();

services.AddRefitClient<ISecureBackupApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<CookieAuthenticationHandler>();

services.AddRefitClient<IAiApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<CookieAuthenticationHandler>();

services.AddRefitClient<ISettingsApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<CookieAuthenticationHandler>();


static string ResolvePasswordResetUrl(string? configuredUrl, string? apiBaseAddress)
{
    var trimmedConfigured = configuredUrl?.Trim();
    if (!string.IsNullOrWhiteSpace(trimmedConfigured))
    {
        if (Uri.TryCreate(trimmedConfigured, UriKind.Absolute, out var absoluteUri))
        {
            return TrimTrailingSlash(absoluteUri.ToString());
        }

        if (!string.IsNullOrWhiteSpace(apiBaseAddress) && Uri.TryCreate(apiBaseAddress.Trim(), UriKind.Absolute, out var baseUri))
        {
            var combinedUri = new Uri(baseUri, trimmedConfigured.TrimStart('/'));
            return TrimTrailingSlash(combinedUri.ToString());
        }
    }

    if (!string.IsNullOrWhiteSpace(apiBaseAddress) && Uri.TryCreate(apiBaseAddress.Trim(), UriKind.Absolute, out var fallbackBase))
    {
        var combinedUri = new Uri(fallbackBase, "reset-password");
        return TrimTrailingSlash(combinedUri.ToString());
    }

    throw new InvalidOperationException("Email password reset URL is not configured and no API base address fallback is available.");
}

static string TrimTrailingSlash(string value)
{
    if (!string.IsNullOrEmpty(value) && value.EndsWith("/", StringComparison.Ordinal))
    {
        return value[..^1];
    }

    return value;
}


services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LogMyDay API",
        Version = "v1",
        Description = "LogMyDay API with Cookie Authentication"
    });

    options.AddSecurityDefinition("cookie", new OpenApiSecurityScheme
    {
        Name = "lmd.auth",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Description = "Cookie-based authentication"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "cookie"
                }
            },
            Array.Empty<string>()
        }
    });
});


var app = builder.Build();

// Configure HTTPS security
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Enhanced HSTS configuration for production
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Force HTTPS redirection for all environments
app.UseHttpsRedirection();

// Add request logging middleware with sensitive data masking
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("========== HTTP REQUEST ==========");
    logger.LogInformation("= Method: {Method}", context.Request.Method);
    logger.LogInformation("= Path: {Path}", context.Request.Path);
    logger.LogInformation("= QueryString: {QueryString}", context.Request.QueryString);
    
    // Log headers with sensitive fields masked
    var sensitiveHeaders = new[] { "Authorization", "Cookie", "X-CSRF-Token", "Set-Cookie" };
    var safeHeaders = context.Request.Headers
        .Where(h => !sensitiveHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
        .Select(h => $"{h.Key}={string.Join(",", h.Value.ToArray())}");
    logger.LogInformation("= Headers: {Headers}", string.Join(", ", safeHeaders));

    // DO NOT log form data - it may contain passwords and other sensitive information
    // Only log that form data was present
    if (context.Request.HasFormContentType && context.Request.Method == "POST")
    {
        logger.LogInformation("= Form Data: [REDACTED - contains sensitive information]");
    }

    logger.LogInformation("= User Authenticated: {IsAuthenticated}", context.User?.Identity?.IsAuthenticated);
    logger.LogInformation("= User Name: {UserName}", context.User?.Identity?.Name ?? "null");

    await next();

    logger.LogInformation("= Response Status: {StatusCode}", context.Response.StatusCode);
    
    // Log response headers with sensitive fields masked
    var safeResponseHeaders = context.Response.Headers
        .Where(h => !sensitiveHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
        .Select(h => $"{h.Key}={string.Join(",", h.Value.ToArray())}");
    logger.LogInformation("= Response Headers: {ResponseHeaders}", string.Join(", ", safeResponseHeaders));
    logger.LogInformation("========== HTTP REQUEST END ==========");
});

// Enable rate limiting
app.UseRateLimiter();

// Add security headers
app.Use(async (context, next) =>
{
    // Enforce HTTPS and prevent downgrade attacks
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    // Prevent clickjacking
    context.Response.Headers["X-Frame-Options"] = "DENY";
    // Prevent MIME-type sniffing
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    // XSS protection
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    // Referrer policy
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    await next();
});

app.MapStaticAssets();
app.UseRouting();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Map health check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Seed the database with initial admin user (skip in Test environment)
if (builder.Environment.EnvironmentName != "Test")
{
    using (var scope = app.Services.CreateScope())
    {
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync();
    }
}

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
