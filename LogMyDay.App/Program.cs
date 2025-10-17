using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Email;
using LogMyDay.Api.Security;
using LogMyDay.App.Authentication;
using LogMyDay.App.Components;
using LogMyDay.Shared.Interfaces;
using LogMyDay.App.Services;
using LogMyDay.Shared.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Refit;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

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

services.AddDbContext<LogMyDayDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add memory cache for authentication tracking
services.AddMemoryCache();

// Add HttpContextAccessor for Blazor components
services.AddHttpContextAccessor();

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
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.LoginPath = "/login"; // Redirect to Blazor login page
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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Configure rate limiting for brute-force protection
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
    
    // Global rejection behavior
    options.RejectionStatusCode = 429; // Too Many Requests
});

services.AddRazorComponents().AddInteractiveServerComponents();

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

// UI services
services.AddScoped<LogMyDay.App.Services.IPageTitleService, LogMyDay.App.Services.PageTitleService>();
services.AddScoped<IUserPreferencesService, UserPreferencesService>();

// Authentication and user services
services.AddScoped<IPasswordHasher, Argon2IdPasswordHasher>();
services.AddScoped<IUserService, UserService>();
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
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

// Add comprehensive request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("=== HTTP REQUEST ===");
    logger.LogInformation("Method: {Method}", context.Request.Method);
    logger.LogInformation("Path: {Path}", context.Request.Path);
    logger.LogInformation("QueryString: {QueryString}", context.Request.QueryString);
    logger.LogInformation("Headers: {Headers}", string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value.ToArray())}")));
    
    if (context.Request.HasFormContentType && context.Request.Method == "POST")
    {
        // Read form data for POST requests
        var form = await context.Request.ReadFormAsync();
        logger.LogInformation("Form Data: {FormData}", string.Join(", ", form.Select(f => $"{f.Key}={f.Value}")));
    }
    
    logger.LogInformation("User Authenticated: {IsAuthenticated}", context.User?.Identity?.IsAuthenticated);
    logger.LogInformation("User Name: {UserName}", context.User?.Identity?.Name ?? "null");
    
    await next();
    
    logger.LogInformation("Response Status: {StatusCode}", context.Response.StatusCode);
    logger.LogInformation("Response Headers: {ResponseHeaders}", string.Join(", ", context.Response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value.ToArray())}")));
    logger.LogInformation("=== HTTP REQUEST END ===");
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

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Seed the database with initial admin user
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    await seeder.SeedAsync();
}

app.Run();
