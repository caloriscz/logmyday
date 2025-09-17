using LogMyDay.Api.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LogMyDay.Api.Infrastructure.Email;

public class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string email, string? displayName, string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        ValidateOptions();

        var resetLink = BuildResetLink(token);
        var message = CreateMessage(email, displayName, resetLink);

        using var client = new SmtpClient();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Determine the correct secure socket option based on port and configuration
            var secureOption = DetermineSecureSocketOption(_options.SmtpPort, _options.UseSsl);
            await client.ConnectAsync(_options.SmtpServer, _options.SmtpPort, secureOption, cancellationToken);

            var userName = string.IsNullOrWhiteSpace(_options.UserName) ? _options.SenderEmail : _options.UserName;
            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await client.AuthenticateAsync(userName, _options.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);

            _logger.LogInformation("Password reset email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpServer))
        {
            throw new InvalidOperationException("SMTP server is not configured.");
        }

        if (_options.SmtpPort <= 0)
        {
            throw new InvalidOperationException("SMTP port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(_options.SenderEmail))
        {
            throw new InvalidOperationException("Sender email is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.PasswordResetUrl))
        {
            throw new InvalidOperationException("Password reset URL is not configured.");
        }
    }

    private MimeMessage CreateMessage(string email, string? displayName, string resetLink)
    {
        var fromName = string.IsNullOrWhiteSpace(_options.SenderName) ? _options.SenderEmail : _options.SenderName;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, _options.SenderEmail));

        var toMailbox = string.IsNullOrWhiteSpace(displayName)
            ? MailboxAddress.Parse(email)
            : new MailboxAddress(displayName, email);
        message.To.Add(toMailbox);
        message.Subject = "Reset your LogMyDay password";

        var builder = new BodyBuilder
        {
            TextBody = $"We received a request to reset your LogMyDay password.\n\n"
                + $"To choose a new password, open the link below in your browser:\n{resetLink}\n\n"
                + "If you did not request this reset, you can safely ignore this email.",
            
            HtmlBody = $"<p>We received a request to reset your <strong>LogMyDay</strong> password.</p>"
                + $"<p><a href=\"{resetLink}\">Click here to reset your password</a></p>"
                + $"<p>If you did not request this reset, you can safely ignore this email.</p>",
        };

        message.Body = builder.ToMessageBody();
        return message;
    }

    private string BuildResetLink(string token)
    {
        var separator = _options.PasswordResetUrl.Contains('?') ? '&' : '?';
        return $"{_options.PasswordResetUrl}{separator}token={Uri.EscapeDataString(token)}";
    }

    private static SecureSocketOptions DetermineSecureSocketOption(int port, bool useSsl)
    {
        // Port 587 typically uses STARTTLS (explicit TLS)
        // Port 465 typically uses SSL/TLS (implicit TLS)
        // Port 25 typically uses plain text or STARTTLS when available
        
        return port switch
        {
            587 => SecureSocketOptions.StartTls, // Force STARTTLS for port 587
            465 => SecureSocketOptions.SslOnConnect, // SSL/TLS for port 465
            25 when useSsl => SecureSocketOptions.StartTlsWhenAvailable, // STARTTLS when available for port 25
            25 => SecureSocketOptions.None, // Plain text for port 25 when SSL not requested
            _ => useSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable,
        };
    }
}
