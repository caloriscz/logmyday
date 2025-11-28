using System.Net.Http.Headers;
using System.Text;

namespace LogMyDay.App.Authentication;

public class AuthenticationHeaderHandler : DelegatingHandler
{
    private readonly CredentialStore _credentialStore;
    private readonly ILogger<AuthenticationHeaderHandler> _logger;

    public AuthenticationHeaderHandler(CredentialStore credentialStore, ILogger<AuthenticationHeaderHandler> logger)
    {
        _credentialStore = credentialStore;
        _logger = logger;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        (string Username, string Password)? credentials = _credentialStore.Get();
        if (credentials is (var username, var password))
        {
            var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            _logger.LogDebug("[AuthHandler] Injected Basic Auth for user {Username}", username);
        }
        else
        {
            _logger.LogWarning("[AuthHandler] No credentials present");
        }

        return base.SendAsync(request, cancellationToken);
    }
}