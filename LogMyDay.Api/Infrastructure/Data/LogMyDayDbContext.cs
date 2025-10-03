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
    public DbSet<Quantity> Quantities => Set<Quantity>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<TagOptionList> TagOptionLists => Set<TagOptionList>();
    public DbSet<TagOption> TagOptions => Set<TagOption>();

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

            entity.Property(n => n.DeliveriesOnLastDate)
                .HasDefaultValue(0);

            entity.Property(n => n.LastDeliveryDate)
                .HasColumnType("date");

            entity.Property(n => n.LastDeliverySentAtUtc)
                .HasColumnType("datetime2");

            entity.Property(n => n.NextEligibleSendAfterUtc)
                .HasColumnType("datetime2");
        });

        modelBuilder.Entity<Quantity>(entity =>
        {
            entity.HasOne(q => q.BaseUnit)
                .WithMany()
                .HasForeignKey(q => q.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasOne(u => u.Quantity)
                .WithMany()
                .HasForeignKey(u => u.QuantityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TagOptionList>(entity =>
        {
            entity.HasMany(l => l.Options)
                .WithOne(o => o.OptionList)
                .HasForeignKey(o => o.OptionListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasOne(t => t.Unit)
                .WithMany()
                .HasForeignKey(t => t.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.OptionList)
                .WithMany()
                .HasForeignKey(t => t.OptionListId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.SeedData();
    }
}