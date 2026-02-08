namespace LogMyDay.Shared.Attributes;

/// <summary>
/// Client context for route filtering.
/// </summary>
public enum ClientContext
{
    /// <summary>
    /// Route is available to all clients (Web and Mobile).
    /// </summary>
    All,

    /// <summary>
    /// Route is available only to Web clients.
    /// </summary>
    Web,

    /// <summary>
    /// Route is available only to Mobile clients.
    /// </summary>
    Mobile
}

/// <summary>
/// Marks a Blazor page as AI-navigable with metadata for route discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AiNavigableRouteAttribute : Attribute
{
    /// <summary>
    /// Gets the display label for the route.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the description of the route.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets whether the route requires admin privileges.
    /// </summary>
    public bool RequiresAdmin { get; init; }

    /// <summary>
    /// Gets the client context for this route (Web, Mobile, or All).
    /// </summary>
    public ClientContext ClientContext { get; init; } = ClientContext.All;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiNavigableRouteAttribute"/> class.
    /// </summary>
    /// <param name="label">The display label for the route.</param>
    /// <param name="description">The description of the route.</param>
    public AiNavigableRouteAttribute(string label, string description)
    {
        Label = label;
        Description = description;
    }
}
