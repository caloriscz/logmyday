namespace LogMyDay.Api.Infrastructure.Data;

using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// The Settings table has the same shape in the platform database (the installation's settings)
/// and in a user's database (the flags of that database's one-time migrations).
/// </summary>
internal static class SettingModel
{
    public static void Configure(EntityTypeBuilder<Setting> entity)
    {
        entity.HasKey(e => e.Key);
        entity.HasIndex(e => e.Key).IsUnique().HasDatabaseName("IX_LogMyDay_Settings_Key");
        entity.Property(e => e.Key).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Value).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(500);
    }
}
