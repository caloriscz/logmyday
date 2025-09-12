namespace LogMyDay.Domain.Entities;

public sealed class PasswordReset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Token { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? UsedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User? User { get; set; }
}
