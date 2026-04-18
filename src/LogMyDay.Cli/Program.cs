using Cocona;
using LogMyDay.Cli.Commands;
using LogMyDay.Cli.Services;
using Microsoft.Extensions.DependencyInjection;

var builder = CoconaApp.CreateBuilder(args);

builder.Services.AddSingleton<ICredentialStore, WindowsCredentialStore>();
builder.Services.AddSingleton<ConfigManager>();
builder.Services.AddSingleton<CliApiContext>();
builder.Services.AddTransient<CliAuthHandler>();
builder.Services.AddHttpClient("lmd-api")
    .AddHttpMessageHandler<CliAuthHandler>();
builder.Services.AddSingleton<ApiClientFactory>();
builder.Services.AddTransient<TagResolver>();
builder.Services.AddTransient<BatchImporter>();
builder.Services.AddSingleton<ExtensionManager>();

var app = builder.Build();

app.AddCommands<AccountCommands>();
app.AddSubCommand("backup", x => x.AddCommands<BackupCommands>());
app.AddSubCommand("report", x => x.AddCommands<ReportCommands>());
app.AddSubCommand("activities", x => x.AddCommands<ActivityCommands>());
app.AddSubCommand("tags", x => x.AddCommands<TagCommands>());
app.AddSubCommand("extensions", x => x.AddCommands<ExtensionCommands>());

app.Run();
