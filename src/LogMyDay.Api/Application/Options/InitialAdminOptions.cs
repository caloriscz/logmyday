namespace LogMyDay.Api.Application.Options;

/// <summary>
/// The first admin, whom the first start of a new installation creates (section "Admin"). Both
/// settings are required while the installation has no account, so that no installation starts
/// with a known account; they are ignored once an account exists.
/// </summary>
public class InitialAdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>As for every password in LogMyDay.</summary>
    public const int MinimumPasswordLength = 10;

    /// <summary>The first admin's email, which is also the sign-in name.</summary>
    public string? Email { get; set; }

    /// <summary>The first admin's password.</summary>
    public string? InitialPassword { get; set; }
}
