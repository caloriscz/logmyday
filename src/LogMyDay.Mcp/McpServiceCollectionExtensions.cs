using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace LogMyDay.Mcp;

public static class McpServiceCollectionExtensions
{
    public const string ServerInstructions =
        "LogMyDay is a personal activity log: the user records values (numbers, yes/no, ratings, text) " +
        "against tags, and plans with reminders and todo lists. Call server_info first for the user's " +
        "culture and time zone, the value encoding per input type, and the conventions. Dates are " +
        "yyyy-MM-dd; date-times are naive local. Destructive tools require an exact confirm sentinel " +
        "named in their description; never guess one — ask the user. Report numbers only from tool results.";

    /// <summary>
    /// Registers the LogMyDay MCP server. Tools are registered per type on purpose — never from the
    /// assembly — so nothing becomes callable without being deliberately listed here.
    /// </summary>
    public static IServiceCollection AddLogMyDayMcp(this IServiceCollection services)
    {
        services.AddScoped<McpUserContext>();
        services.AddScoped<TagLookup>();
        services.AddScoped<UserClock>();
        services.AddSingleton<DestructiveBudget>();

        services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = McpSchemaVersion.ServerName,
                    Title = "LogMyDay",
                    Version = ServerInfoTools.AppVersion()
                };
                options.ServerInstructions = ServerInstructions;
                options.ScopeRequests = true;
            })
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .AddAuthorizationFilters()
            .WithRequestFilters(filters => filters.AddCallToolFilter(McpToolFilters.AuditAndMapErrors))
            .WithTools<ServerInfoTools>(McpJson.Options)
            .WithTools<TagTools>(McpJson.Options)
            .WithTools<ActivityTools>(McpJson.Options)
            .WithTools<TagGroupTools>(McpJson.Options)
            .WithTools<OptionListTools>(McpJson.Options)
            .WithTools<UnitTools>(McpJson.Options)
            .WithTools<ColorSchemeTools>(McpJson.Options)
            .WithTools<InputTypeTools>(McpJson.Options);

        return services;
    }
}
