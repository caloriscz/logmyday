namespace LogMyDay.Api.Infrastructure.Data;

using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// The installation's own data: who may sign in and how (users, API keys, password resets) and
/// the installation's settings. Everything a user logs lives in <see cref="LogMyDayDbContext"/>.
/// Sign-in has to find the user before anyone knows whose data to open, so these tables can't
/// live with the users' data.
/// </summary>
public class PlatformDbContext : DbContext
{
    public PlatformDbContext(DbContextOptions<PlatformDbContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Setting> Settings => Set<Setting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>().ToTable("LogMyDay_Users");
        modelBuilder.Entity<PasswordReset>().ToTable("LogMyDay_PasswordResets");
        modelBuilder.Entity<ApiKey>().ToTable("LogMyDay_ApiKeys");
        modelBuilder.Entity<Setting>().ToTable("LogMyDay_Settings");

        modelBuilder.Entity<Setting>(SettingModel.Configure);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("IX_LogMyDay_Users_Email");
        });

        modelBuilder.Entity<PasswordReset>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Token).HasDatabaseName("IX_LogMyDay_PasswordResets_Token");
        });

        // Prefix is indexed but not unique: it is a lookup hint and the hash decides; UserId
        // serves the owner's key list. Deleting a user takes their keys.
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasOne(k => k.User)
                .WithMany()
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(k => k.Prefix).HasDatabaseName("IX_LogMyDay_ApiKeys_Prefix");
            entity.HasIndex(k => k.UserId).HasDatabaseName("IX_LogMyDay_ApiKeys_UserId");

            entity.Property(k => k.Name).HasMaxLength(100).IsRequired();
            entity.Property(k => k.Prefix).HasMaxLength(16).IsRequired();
            entity.Property(k => k.KeyHash).HasMaxLength(64).IsRequired();
            entity.Property(k => k.Scope).HasConversion<int>();
        });
    }
}
