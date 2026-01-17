using Cocona;
using LogMyDay.Manager.Cli.Commands;
using LogMyDay.Manager.Core.Services;
using Microsoft.Extensions.DependencyInjection;

var builder = CoconaApp.CreateBuilder();

// Register core services
builder.Services.AddSingleton<ICredentialService, WindowsCredentialService>();
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IServiceManagerService, WindowsServiceManagerService>();
builder.Services.AddSingleton<IPrerequisiteChecker, PrerequisiteChecker>();
builder.Services.AddSingleton<IInstallationService, InstallationService>();

// Register HTTP client for GitHub API
builder.Services.AddHttpClient("GitHub", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LogMyDay-Manager/1.0");
    client.BaseAddress = new Uri("https://api.github.com");
});

// Register HTTP client for LogMyDay API (configured per-server dynamically)
builder.Services.AddHttpClient("LogMyDayApi");

var app = builder.Build();

app.AddCommands<ManagerCommands>();
app.AddSubCommand("server", x =>
{
    x.AddCommands<ServerCommands>();
});

await app.RunAsync();
