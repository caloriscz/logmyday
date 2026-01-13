namespace LogMyDay.Installer.Models;

public class InstallationConfig
{
    public string InstallPath { get; set; } = @"C:\Program Files\LogMyDay";
    public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.SqlServer;
    public string ConnectionString { get; set; } = string.Empty;
    public string ApiBaseAddress { get; set; } = "https://localhost:7064";
    public EmailConfiguration? Email { get; set; }
    public string ServiceName { get; set; } = "LogMyDayApp";
    public string ServiceDisplayName { get; set; } = "LogMyDay Application";
}

public enum DatabaseProvider
{
    SqlServer,
    SQLite
}

public class EmailConfiguration
{
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "LogMyDay";
}
