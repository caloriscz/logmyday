using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace LogMyDay.App.Authentication;

/// <summary>
/// Where this app listens, for the calls it makes to itself. The Host header of a request is
/// whatever the client wrote, so it names the calls but must not choose where they connect. On
/// Kestrel (Docker, dotnet run) they connect to the app's own port. On IIS the app doesn't know
/// its address; there the calls go to the host of the request, which IIS matched to a binding.
/// </summary>
internal sealed class OwnAddress
{
    private readonly Lazy<IReadOnlyList<Listener>> _listeners;

    public OwnAddress(IServer server)
    {
        _listeners = new Lazy<IReadOnlyList<Listener>>(() => Find(server));
    }

    /// <summary>Whether the app knows where it listens.</summary>
    public bool IsKnown => _listeners.Value.Count > 0;

    /// <summary>The address to call for a request that came to the given one.</summary>
    public Uri For(Uri requested)
    {
        if (!IsKnown)
        {
            return requested;
        }

        var listener = _listeners.Value.FirstOrDefault(l => l.Scheme == requested.Scheme) ?? _listeners.Value[0];

        return new UriBuilder(requested) { Scheme = listener.Scheme, Port = listener.Port }.Uri;
    }

    /// <summary>Connects to the app's own listener on the port, whatever host the address names.</summary>
    public async ValueTask<Stream> Connect(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        EndPoint target = context.DnsEndPoint;
        if (IsKnown)
        {
            target = _listeners.Value.FirstOrDefault(l => l.Port == context.DnsEndPoint.Port)?.Target
                ?? throw new HttpRequestException($"The app does not listen on port {context.DnsEndPoint.Port}.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(target, cancellationToken);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();

            throw;
        }
    }

    private static IReadOnlyList<Listener> Find(IServer server)
    {
        // Behind IIS out of process Kestrel takes requests from IIS only, which has the token.
        var isKestrel = server.GetType().Namespace?.StartsWith("Microsoft.AspNetCore.Server.Kestrel", StringComparison.Ordinal) == true
            && Environment.GetEnvironmentVariable("ASPNETCORE_TOKEN") == null;
        var addresses = isKestrel ? server.Features.Get<IServerAddressesFeature>()?.Addresses : null;

        return addresses?.Select(BindingAddress.Parse).Where(a => !a.IsUnixPipe && !a.IsNamedPipe).Select(Listener.From).ToList() ?? [];
    }

    private sealed record Listener(string Scheme, int Port, EndPoint Target)
    {
        public static Listener From(BindingAddress address)
        {
            var isOneAddress = IPAddress.TryParse(address.Host, out var ip) && !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any);
            EndPoint target = isOneAddress ? new IPEndPoint(ip!, address.Port) : new DnsEndPoint("localhost", address.Port);

            return new Listener(address.Scheme, address.Port, target);
        }
    }
}
