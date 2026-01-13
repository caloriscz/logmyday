using Cocona;
using LogMyDay.Installer.Commands;
using LogMyDay.Installer.Services;
using Microsoft.Extensions.DependencyInjection;
using Refit;

var builder = CoconaApp.CreateBuilder();

// Register services
builder.Services.AddSingleton<ICredentialService, WindowsCredentialService>();
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IServiceManagerService, WindowsServiceManagerService>();
builder.Services.AddSingleton<IPrerequisiteChecker, PrerequisiteChecker>();
builder.Services.AddSingleton<IInstallationService, InstallationService>();

// Register HTTP client for GitHub API
builder.Services.AddHttpClient("GitHub", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LogMyDay-Installer/1.0");
    client.BaseAddress = new Uri("https://api.github.com");
});

// Register HTTP client for LogMyDay API (configured per-server dynamically)
builder.Services.AddHttpClient("LogMyDayApi");

var app = builder.Build();

app.AddCommands<InstallerCommands>();

await app.RunAsync();
