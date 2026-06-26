using ApexCharts;
using LogMyDay.App.Components;
using LogMyDay.App.Extensions;
using LogMyDay.Api.Infrastructure;
using LogMyDay.Shared.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Load Docker secrets from /run/secrets if running in Docker environment
if (builder.Environment.EnvironmentName == "Docker")
{
    builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true, reloadOnChange: false);

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

// Data Protection — keys persisted to disk so sessions survive deployments/restarts
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionPath);

services.AddDataProtection()
    .SetApplicationName("LogMyDay")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

Log.Information("Data Protection configured: Keys will be persisted to {Path}", dataProtectionPath);

services.AddMemoryCache();
services.AddHttpContextAccessor();
services.AddHealthChecks();

services.AddAppAuthentication();
services.AddAppRateLimiting();

services.AddResilientRazorComponents(builder.Configuration, builder.Environment);
services.AddApexCharts();

services.AddControllers()
    .AddJsonOptions(options => JsonSerializationSettings.Configure(options.JsonSerializerOptions));
services.AddEndpointsApiExplorer();

// Centralized API error handling — returns ProblemDetails instead of per-action try/catch.
services.AddProblemDetails();
services.AddExceptionHandler<GlobalExceptionHandler>();

services.AddDatabase(builder.Configuration, builder.Environment);
services.AddApplicationServices(builder.Configuration, builder.Environment);
services.AddRefitClients(builder.Configuration);
services.AddSwagger();

var app = builder.Build();

// API exceptions are turned into ProblemDetails by GlobalExceptionHandler; anything it does not
// handle (e.g. Blazor) falls back to the /Error page.
app.UseExceptionHandler("/Error", createScopeForErrors: true);

// Fill empty-body error responses on API routes (unmatched route, auth 401/403, rate-limit 429,
// etc.) with a consistent ProblemDetails. Scoped to /api so Blazor keeps its own error handling.
// Responses that already carry a body are left untouched by the status-code-pages middleware.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiBranch => apiBranch.UseStatusCodePages(async statusCodeContext =>
    {
        var http = statusCodeContext.HttpContext;
        var problemDetails = http.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = { Status = http.Response.StatusCode }
        });
    }));

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    app.UseSwaggerWithUi();
}

app.UseHttpsRedirection();
app.UseRequestLogging();
app.UseRateLimiter();
app.UseSecurityHeaders();

app.MapStaticAssets();
app.UseRouting();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

if (builder.Environment.EnvironmentName != "Test")
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Application.Interfaces.IDatabaseSeeder>();
    await seeder.SeedAsync();

    var reminderDayBackfill = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Application.Interfaces.IReminderDayBackfill>();
    await reminderDayBackfill.RunAsync();

    var reminderRecurrenceMigration = scope.ServiceProvider.GetRequiredService<LogMyDay.Api.Application.Interfaces.IReminderRecurrenceMigration>();
    await reminderRecurrenceMigration.RunAsync();
}

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
