namespace LogMyDay.Api.Infrastructure.Email;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpServer { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string? UserName { get; set; } = null;

    public string? Password { get; set; } = null;

    public string SenderEmail { get; set; } = string.Empty;

    public string? SenderName { get; set; } = null;

    public string PasswordResetUrl { get; set; } = string.Empty;
}
