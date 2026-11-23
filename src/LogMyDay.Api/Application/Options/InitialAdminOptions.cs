namespace LogMyDay.Api.Application.Options;

/// <summary>
/// The first admin, whom the first start of a new installation creates (section "Admin").
/// </summary>
public class InitialAdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>The first admin's sign-in name.</summary>
    public const string Email = "admin";

    /// <summary>As for every password in LogMyDay.</summary>
    public const int MinimumPasswordLength = 10;

    /// <summary>
    /// The first admin's password. Required while the installation has no account, so that no
    /// installation starts with a known password; ignored once an account exists.
    /// </summary>
    public string? InitialPassword { get; set; }
}
