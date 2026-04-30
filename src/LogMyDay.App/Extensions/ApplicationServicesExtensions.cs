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
        services.AddScoped<ITodoListService, TodoListService>();
        services.AddScoped<ITodoItemService, TodoItemService>();
        services.AddScoped<IExcelExportService, ExportService>();
        services.AddScoped<IEventLogService, EventLogService>();
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

        // Email — PasswordResetUrl is now optional; when absent the reset link is built from the
        // incoming request's scheme and host at send time (see MailKitEmailSender).
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));
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
}
