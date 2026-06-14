namespace LogMyDay.App.Extensions;

/// <summary>
/// Tunable Blazor Server circuit / SignalR resilience values. Longer timeouts and a longer
/// disconnected-circuit retention let transient WebSocket drops resume instead of forcing a full
/// page reload. A host app-pool recycle still ends the circuit (the reload is then unavoidable).
/// </summary>
public sealed class CircuitResilienceOptions
{
    public int DisconnectedRetentionMinutes { get; set; }
    public int ClientTimeoutSeconds { get; set; }
    public int KeepAliveSeconds { get; set; }
    public int HandshakeTimeoutSeconds { get; set; }
    public int MaxRetainedDisconnectedCircuits { get; set; }
}

internal static class BlazorCircuitExtensions
{
    internal static IServiceCollection AddResilientRazorComponents(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        // Environment-aware defaults: shared/production hosting gets longer timeouts and retention so
        // a brief WebSocket hiccup doesn't reload the page. Any value is overridable via "Circuit".
        var options = environment.IsDevelopment()
            ? new CircuitResilienceOptions
            {
                DisconnectedRetentionMinutes = 3,
                ClientTimeoutSeconds = 30,
                KeepAliveSeconds = 15,
                HandshakeTimeoutSeconds = 15,
                MaxRetainedDisconnectedCircuits = 100
            }
            : new CircuitResilienceOptions
            {
                DisconnectedRetentionMinutes = 10,
                ClientTimeoutSeconds = 120,
                KeepAliveSeconds = 15,
                HandshakeTimeoutSeconds = 30,
                MaxRetainedDisconnectedCircuits = 100
            };

        configuration.GetSection("Circuit").Bind(options);

        services.AddRazorComponents()
            .AddInteractiveServerComponents(circuit =>
            {
                circuit.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(options.DisconnectedRetentionMinutes);
                circuit.DisconnectedCircuitMaxRetained = options.MaxRetainedDisconnectedCircuits;
            })
            .AddHubOptions(hub =>
            {
                hub.ClientTimeoutInterval = TimeSpan.FromSeconds(options.ClientTimeoutSeconds);
                hub.KeepAliveInterval = TimeSpan.FromSeconds(options.KeepAliveSeconds);
                hub.HandshakeTimeout = TimeSpan.FromSeconds(options.HandshakeTimeoutSeconds);
            });

        return services;
    }
}
