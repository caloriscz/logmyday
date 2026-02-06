namespace LogMyDay.Api.Application.Services.Ai;

/// <summary>
/// Represents a user-navigable route in the application.
/// </summary>
public sealed record NavigableRoute(
    string Path,
    string Label,
    string Description,
    bool RequiresAdmin = false
);

/// <summary>
/// Service for discovering and managing application routes for AI navigation.
/// </summary>
public interface IRouteDiscoveryService
{
    /// <summary>
    /// Gets all user-navigable routes in the application.
    /// </summary>
    List<NavigableRoute> GetNavigationMap();
}

/// <summary>
/// Implementation of route discovery service with static route registry.
/// </summary>
public sealed class RouteDiscoveryService : IRouteDiscoveryService
{
    private static readonly List<NavigableRoute> Routes =
    [
        new("/", "Home", "View today's activities and required tag reminders"),
        new("/activities", "Activities", "Browse and manage all logged activities"),
        new("/tags", "Tags", "View and manage activity tags with input types"),
        new("/tags/new", "Create Tag", "Create a new activity tag"),
        new("/option-lists", "Option Lists", "Manage predefined value lists for tags"),
        new("/units", "Units", "Manage measurement units for numeric tags"),
        new("/insights", "Insights", "View activity trends and data distribution"),
        new("/insights/journal", "Journal", "Read activity journal entries"),
        new("/calendar", "Calendar", "View activities in calendar format"),
        new("/calendar-linear", "Linear Calendar", "View activities in linear timeline"),
        new("/statistics", "Statistics", "Analyze numeric tag statistics and streaks"),
        new("/charts", "Charts", "Visualize tag data with interactive charts"),
        new("/reports", "Reports", "Generate activity reports"),
        new("/backup", "Backup", "Export and import activity data"),
        new("/profile", "Profile", "Manage user profile and preferences"),
        new("/users", "Users", "Manage user accounts", RequiresAdmin: true),
        new("/settings", "Settings", "Configure application settings", RequiresAdmin: true),
        new("/assistant", "AI Assistant", "Chat with the AI assistant")
    ];

    public List<NavigableRoute> GetNavigationMap() => Routes;
}
