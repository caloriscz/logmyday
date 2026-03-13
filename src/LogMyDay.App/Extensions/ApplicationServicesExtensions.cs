using ApexCharts;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Application.Services.Ai;
using LogMyDay.Api.Infrastructure.Email;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Api.Security;
using LogMyDay.App.Authentication;
using LogMyDay.App.Services;
using LogMyDay.App.Services.Charts;
using LogMyDay.Shared.Serialization;
using Microsoft.Extensions.AI;
using OpenAI;
using Serilog;

namespace LogMyDay.App.Extensions;

internal static class ApplicationServicesExtensions
{
    internal static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Core services
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IInputTypeService, InputTypeService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<ITagOptionListService, TagOptionListService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IScanMappingService, ScanMappingService>();
        services.AddScoped<ITagGroupService, TagGroupService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<IExcelExportService, ExportService>();
        services.AddSingleton<ISettingProtector, SettingProtector>();
        services.AddScoped<ISettingsService, SettingsService>();

        // Repository layer
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IUnitRepository, UnitRepository>();
        services.AddScoped<IQuantityRepository, QuantityRepository>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

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
        services.AddSingleton<CredentialStore>();

        // Email
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .PostConfigure(options =>
            {
                options.PasswordResetUrl = ResolvePasswordResetUrl(options.PasswordResetUrl, configuration["Api:BaseAddress"]);
                Log.Information("Password reset URL configured to {PasswordResetUrl}", options.PasswordResetUrl);
            });
        services.AddScoped<IEmailSender, MailKitEmailSender>();

        // AI services
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName));
        services.AddSingleton<IAiChatClientFactory, AiChatClientFactory>();
        services.AddSingleton<IRouteDiscoveryService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RouteDiscoveryService>>();
            var assemblies = new[] { typeof(Program).Assembly };
            return new RouteDiscoveryService(assemblies, logger);
        });
        services.AddScoped<AiToolFunctions>();
        services.AddScoped<IAiAssistantService, AiAssistantService>();

        Log.Information("AI assistant services configured (availability determined at runtime)");

        return services;
    }

    private static string ResolvePasswordResetUrl(string? configuredUrl, string? apiBaseAddress)
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

    private static string TrimTrailingSlash(string value)
    {
        if (!string.IsNullOrEmpty(value) && value.EndsWith("/", StringComparison.Ordinal))
        {
            return value[..^1];
        }

        return value;
    }
}
