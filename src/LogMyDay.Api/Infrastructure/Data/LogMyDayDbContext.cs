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
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ScanMapping> ScanMappings => Set<ScanMapping>();
    public DbSet<TagGroup> TagGroups => Set<TagGroup>();
    public DbSet<EventLog> EventLogs => Set<EventLog>();
    public DbSet<EventLogDetail> EventLogDetails => Set<EventLogDetail>();
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

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
        modelBuilder.Entity<Setting>().ToTable("LogMyDay_Settings");
        modelBuilder.Entity<ScanMapping>().ToTable("LogMyDay_ScanMappings");
        modelBuilder.Entity<TagGroup>().ToTable("LogMyDay_TagGroups");
        modelBuilder.Entity<EventLog>().ToTable("LogMyDay_EventLogs");
        modelBuilder.Entity<EventLogDetail>().ToTable("LogMyDay_EventLogDetails");
        modelBuilder.Entity<TodoList>().ToTable("LogMyDay_TodoLists");
        modelBuilder.Entity<TodoItem>().ToTable("LogMyDay_TodoItems");

        // Configure Setting entity
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.HasIndex(e => e.Key).IsUnique().HasDatabaseName("IX_LogMyDay_Settings_Key");
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
            entity.Property(n => n.NudgeInterval).HasDefaultValue(new TimeSpan(0, 15, 0));
            entity.Property(n => n.DeliveriesOnLastDate).HasDefaultValue(0);

            if (Database.IsSqlServer())
            {
                entity.Property(n => n.DateCreated).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(n => n.LastDeliveryDate).HasColumnType("date");
                entity.Property(n => n.LastDeliverySentAtUtc).HasColumnType("datetime2");
                entity.Property(n => n.NextEligibleSendAfterUtc).HasColumnType("datetime2");
            }
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
            entity.HasOne(t => t.Group).WithMany().HasForeignKey(t => t.GroupId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TagGroup>(entity =>
        {
            entity.Property(g => g.Name).HasMaxLength(100).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(500);
            entity.HasIndex(g => g.UserId).HasDatabaseName("IX_LogMyDay_TagGroups_UserId");
        });

        modelBuilder.Entity<ScanMapping>(entity =>
        {
            entity.HasOne(s => s.Tag)
                .WithMany()
                .HasForeignKey(s => s.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => new { s.UserId, s.CodeValue })
                .IsUnique()
                .HasDatabaseName("IX_LogMyDay_ScanMappings_UserId_CodeValue");

            entity.HasIndex(s => s.CodeValue)
                .HasDatabaseName("IX_LogMyDay_ScanMappings_CodeValue");

            entity.Property(s => s.IsActive).HasDefaultValue(true);
            entity.Property(s => s.CodeValue).HasMaxLength(512).IsRequired();
            entity.Property(s => s.DisplayName).HasMaxLength(200);
        });

        modelBuilder.Entity<EventLog>(entity =>
        {
            entity.HasOne(e => e.Detail)
                .WithOne(d => d.EventLog)
                .HasForeignKey<EventLogDetail>(d => d.EventLogId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.CreatedUtc })
                .HasDatabaseName("IX_LogMyDay_EventLogs_UserId_CreatedUtc");

            entity.HasIndex(e => e.Level)
                .HasDatabaseName("IX_LogMyDay_EventLogs_Level");

            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<TodoList>(entity =>
        {
            entity.HasMany(l => l.Items)
                .WithOne(i => i.List)
                .HasForeignKey(i => i.ListId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(l => l.UserId).HasDatabaseName("IX_LogMyDay_TodoLists_UserId");

            entity.Property(l => l.Name).HasMaxLength(200).IsRequired();
            entity.Property(l => l.DisplayOrder).HasDefaultValue(0);

            if (Database.IsSqlServer())
            {
                entity.Property(l => l.DateCreated).HasDefaultValueSql("GETUTCDATE()");
            }
        });

        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.HasOne(i => i.CompletionTag)
                .WithMany()
                .HasForeignKey(i => i.CompletionTagId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(i => i.Title).HasMaxLength(500).IsRequired();
            entity.Property(i => i.IsDone).HasDefaultValue(false);
            entity.Property(i => i.DisplayOrder).HasDefaultValue(0);

            if (Database.IsSqlServer())
            {
                entity.Property(i => i.DateCreated).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(i => i.StartDate).HasColumnType("date");
                entity.Property(i => i.DueDate).HasColumnType("date");
                entity.Property(i => i.NotifyAt).HasColumnType("time");
                entity.Property(i => i.DoneAt).HasColumnType("datetime2");
                entity.Property(i => i.MonitorFromDate).HasColumnType("date");
                entity.Property(i => i.MonitorToDate).HasColumnType("date");
            }
        });

        modelBuilder.SeedData();
    }
}