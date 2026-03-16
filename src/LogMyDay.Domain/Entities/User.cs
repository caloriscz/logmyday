namespace LogMyDay.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; } // unique, normalized
    public string? DisplayName { get; set; }
    public required string PasswordHash { get; set; } // Argon2id encoded string with salt
    public bool IsAdmin { get; set; }
    public string Culture { get; set; } = "en-US";
    public string TimeZone { get; set; } = "Europe/Vienna";
    public string ActivityDisplayType { get; set; } = "daily";
    public string ActivitySortOrder { get; set; } = "desc";
    public string ActivityPeriodSort { get; set; } = "desc";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
