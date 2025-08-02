using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.App.Authentication;
using LogMyDay.App.Components;
using LogMyDay.Shared.Interfaces;
using Microsoft.AspNetCore.Authentication;
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

services.Configure<BasicAuthOptions>(builder.Configuration.GetSection("Auth:Basic"));
services.AddDbContext<LogMyDayDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Add memory cache for authentication tracking
services.AddMemoryCache();

services.AddAuthentication(BasicAuthConstants.Scheme).AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>(
        BasicAuthConstants.Scheme, null);

services.AddAuthorization();

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
services.AddScoped<LogMyDay.Api.Authentication.AuthAttemptTracker>();

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


services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LogMyDay API",
        Version = "v1"
    });

    options.AddSecurityDefinition("basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Enter your username and password for Basic Authentication"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "basic"
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

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
