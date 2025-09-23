namespace LogMyDay.Api.Infrastructure.Data;

using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public class LogMyDayDbContext : DbContext
{
    public LogMyDayDbContext(DbContextOptions<LogMyDayDbContext> options)
        : base(options) { }

    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<InputType> InputTypes => Set<InputType>();
    public DbSet<Pattern> Patterns => Set<Pattern>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");
        });

        // Configure PasswordReset entity
        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Token)
                .HasDatabaseName("IX_PasswordResets_Token");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.Tag)
                .WithMany(t => t.Notifications)
                .HasForeignKey(n => n.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(n => n.MaxNudges)
                .HasDefaultValue(3);

            entity.Property(n => n.IsActive)
                .HasDefaultValue(true);

            entity.Property(n => n.DateCreated)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(n => n.NudgeInterval)
                .HasDefaultValue(new TimeSpan(0, 15, 0));
        });

        modelBuilder.SeedData();
    }
}