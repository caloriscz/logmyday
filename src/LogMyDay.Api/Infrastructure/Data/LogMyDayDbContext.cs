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
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Add LogMyDay_ prefix to all table names to avoid collisions
        modelBuilder.Entity<Activity>().ToTable("LogMyDay_Activities");
        modelBuilder.Entity<Tag>().ToTable("LogMyDay_Tags");
        modelBuilder.Entity<InputType>().ToTable("LogMyDay_InputTypes");
        modelBuilder.Entity<Pattern>().ToTable("LogMyDay_Patterns");
        modelBuilder.Entity<User>().ToTable("LogMyDay_Users");
        modelBuilder.Entity<PasswordReset>().ToTable("LogMyDay_PasswordResets");
        modelBuilder.Entity<Notification>().ToTable("LogMyDay_Notifications");
        modelBuilder.Entity<Quantity>().ToTable("LogMyDay_Quantities");
        modelBuilder.Entity<Unit>().ToTable("LogMyDay_Units");
        modelBuilder.Entity<TagOptionList>().ToTable("LogMyDay_TagOptionLists");
        modelBuilder.Entity<TagOption>().ToTable("LogMyDay_TagOptions");
        modelBuilder.Entity<AppSetting>().ToTable("LogMyDay_AppSettings");

        // Configure AppSetting entity
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => e.Key).IsUnique().HasDatabaseName("IX_LogMyDay_AppSettings_Key");
            entity.Property(e => e.Key).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
        });

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("IX_LogMyDay_Users_Email");
        });

        // Configure PasswordReset entity
        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Token).HasDatabaseName("IX_LogMyDay_PasswordResets_Token");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.Tag)
                .WithMany(t => t.Notifications)
                .HasForeignKey(n => n.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(n => n.MaxNudges)
                .HasDefaultValue(3);

            entity.Property(n => n.IsActive).HasDefaultValue(true);
            entity.Property(n => n.DateCreated).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(n => n.NudgeInterval).HasDefaultValue(new TimeSpan(0, 15, 0));
            entity.Property(n => n.DeliveriesOnLastDate).HasDefaultValue(0);
            entity.Property(n => n.LastDeliveryDate).HasColumnType("date");
            entity.Property(n => n.LastDeliverySentAtUtc).HasColumnType("datetime2");
            entity.Property(n => n.NextEligibleSendAfterUtc).HasColumnType("datetime2");
        });

        modelBuilder.Entity<Quantity>(entity =>
        {
            entity.HasOne(q => q.BaseUnit).WithMany().HasForeignKey(q => q.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasOne(u => u.Quantity).WithMany().HasForeignKey(u => u.QuantityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TagOptionList>(entity =>
        {
            entity.HasMany(l => l.Options).WithOne(o => o.OptionList).HasForeignKey(o => o.OptionListId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasOne(t => t.Unit).WithMany().HasForeignKey(t => t.UnitId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(t => t.OptionList).WithMany().HasForeignKey(t => t.OptionListId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.SeedData();
    }
}