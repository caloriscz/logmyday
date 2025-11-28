namespace LogMyDay.Api.Application.Interfaces;

public interface IEmailSender
{
    Task SendPasswordResetEmailAsync(string email, string? displayName, string token, CancellationToken cancellationToken);
}
