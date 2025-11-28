namespace LogMyDay.Api.Authentication;

public class BasicAuthOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Guid UserId { get; set; }
}