using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.App.Authentication;
using LogMyDay.Shared.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Refit;
using Serilog;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

var services = builder.Services;

services.AddDbContext<LogMyDayDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add memory cache for authentication tracking
services.AddMemoryCache();

// Add HttpContextAccessor for Blazor components
services.AddHttpContextAccessor();

// Configure cookie authentication
services.AddAuthentication("lmd-cookie")
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
    });

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
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
services.AddEndpointsApiExplorer();

services.AddScoped<IActivityService, ActivityService>();
services.AddScoped<ITagService, TagService>();
services.AddScoped<IBackupService, BackupService>();
services.AddScoped<IExcelExportService, ExcelExportService>();

// Authentication and user services
services.AddScoped<IPasswordHasher, Argon2IdPasswordHasher>();
services.AddScoped<IUserService, UserService>();
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

// Keep existing credential store for Blazor Server
services.AddSingleton<CredentialStore>();
services.AddTransient<AuthenticationHeaderHandler>();


services.AddRefitClient<IActivityApi>()
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    })
    .AddHttpMessageHandler<AuthenticationHeaderHandler>();

// Add new authentication API clients
services.AddRefitClient<IAuthApi>()
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    });

services.AddRefitClient<IUsersApi>()
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    });

services.AddRefitClient<IAccountApi>()
    .ConfigureHttpClient(c =>
    {
        var baseAddress = builder.Configuration["Api:BaseAddress"];
        if (string.IsNullOrEmpty(baseAddress))
        {
            throw new InvalidOperationException("API base address is not configured.");
        }
        c.BaseAddress = new Uri(baseAddress);
    });


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

app.MapRazorComponents<LogMyDay.App.Components.App>().AddInteractiveServerRenderMode();

// Seed the database with initial admin user
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    await seeder.SeedAsync();
}

app.Run();
