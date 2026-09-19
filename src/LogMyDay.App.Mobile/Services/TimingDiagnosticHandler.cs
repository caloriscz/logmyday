using System.Diagnostics;
using System.Globalization;
using LogMyDay.App.Mobile.Services.Diagnostics;

namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Records the client/server/network split for every API call into the admin-gated diagnostic
/// store, so mobile latency can be attributed rather than guessed at.
///
/// The server reports its own cost in a Server-Timing header (see the request-logging middleware);
/// whatever the client measures on top of that is connection setup plus transfer. Every mobile
/// request carries Basic credentials, so `argon2` is the per-request password verification the
/// cookie-based web client never pays.
///
/// Must be the outermost handler in the pipeline so its stopwatch spans the TLS handshake too.
/// Uses the static <see cref="DiagnosticStore.Instance"/> — the same pattern the OS-constructed
/// receivers use — rather than injecting IDiagnosticStore, which would take a construction-time
/// dependency back through IApiClientProvider to the very factory building this handler.
/// </summary>
public class TimingDiagnosticHandler : DelegatingHandler
{
    /// <summary>A call slower than this is worth a row even when it succeeded.</summary>
    public const double SlowCallMs = 750;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var diag = DiagnosticStore.Instance;

        // Never measure the diagnostic outbox's own POSTs: each flushed row would create a new row
        // and the backlog could never drain.
        if (diag is not { Enabled: true } || IsOutboxRequest(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var start = Stopwatch.GetTimestamp();
        HttpResponseMessage? response = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken);

            return response;
        }
        finally
        {
            // Recording must never affect the call it is measuring.
            try
            {
                var clientMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                Record(diag, request, response, clientMs);
            }
            catch (Exception)
            {
                // ignored — diagnostics are best-effort
            }
        }
    }

    private static bool IsOutboxRequest(HttpRequestMessage request)
    {
        return request.Method == HttpMethod.Post
            && request.RequestUri?.AbsolutePath.TrimEnd('/').EndsWith("/api/eventlogs", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void Record(IDiagnosticStore diag, HttpRequestMessage request, HttpResponseMessage? response, double clientMs)
    {
        // A fast, successful call says nothing; only failures and slow calls explain anything.
        if (response is { IsSuccessStatusCode: true } && clientMs < SlowCallMs)
        {
            return;
        }

        var status = response is null ? "failed" : ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var serverMs = ReadTiming(response, "total");
        var argon2Ms = ReadTiming(response, "argon2");
        var lookupMs = ReadTiming(response, "lookup");

        // Everything the client saw that the server did not account for: DNS, TCP, TLS, transfer.
        var networkMs = serverMs.HasValue ? clientMs - serverMs.Value : (double?)null;

        diag.Record("perf-diag", string.Create(CultureInfo.InvariantCulture,
            $"event=request method={request.Method} path={request.RequestUri?.AbsolutePath} status={status} " +
            $"clientMs={clientMs:0} serverMs={Format(serverMs)} argon2Ms={Format(argon2Ms)} " +
            $"lookupMs={Format(lookupMs)} networkMs={Format(networkMs)}"));
    }

    private static string Format(double? value) => value?.ToString("0", CultureInfo.InvariantCulture) ?? "n/a";

    /// <summary>Pulls one metric out of "argon2;dur=186.1, lookup;dur=3.2, total;dur=310.7".</summary>
    private static double? ReadTiming(HttpResponseMessage? response, string metric)
    {
        if (response is null || !response.Headers.TryGetValues("Server-Timing", out var values))
        {
            return null;
        }

        foreach (var header in values)
        {
            foreach (var part in header.Split(','))
            {
                var trimmed = part.Trim();

                if (!trimmed.StartsWith(metric + ";", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var durIndex = trimmed.IndexOf("dur=", StringComparison.OrdinalIgnoreCase);

                if (durIndex >= 0
                    && double.TryParse(trimmed[(durIndex + 4)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }
}
