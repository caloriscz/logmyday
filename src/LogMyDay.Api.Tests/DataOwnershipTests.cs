using LogMyDay.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Every table is either the installation's (platform database) or a user's (that user's
/// database). The ownership map has to know each one, or the split of an installation would leave
/// its rows behind.
/// </summary>
public class DataOwnershipTests
{
    private static LogMyDayDbContext Data() =>
        new(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite("Data Source=:memory:").Options);

    private static PlatformDbContext Platform() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite("Data Source=:memory:").Options);

    [Fact]
    public void EveryEntityOfBothModels_IsClassified()
    {
        using var data = Data();
        using var platform = Platform();

        var entities = data.Model.GetEntityTypes().Concat(platform.Model.GetEntityTypes()).Select(e => e.ClrType).Distinct();

        var missing = entities.Where(e => !DataOwnership.Map.ContainsKey(e)).Select(e => e.Name).ToList();
        Assert.True(missing.Count == 0, "Not in DataOwnership.Map: " + string.Join(", ", missing));
    }

    [Fact]
    public void TheMap_HasNoEntityOutsideTheModels()
    {
        using var data = Data();
        using var platform = Platform();

        var entities = data.Model.GetEntityTypes().Concat(platform.Model.GetEntityTypes()).Select(e => e.ClrType).ToHashSet();

        Assert.Empty(DataOwnership.Map.Keys.Where(e => !entities.Contains(e)).Select(e => e.Name));
    }

    [Fact]
    public void PlatformEntities_AreInThePlatformModelOnly_AndTheRestInTheDataModelOnly()
    {
        using var data = Data();
        using var platform = Platform();

        foreach (var (type, entry) in DataOwnership.Map)
        {
            var inData = data.Model.FindEntityType(type) != null;
            var inPlatform = platform.Model.FindEntityType(type) != null;

            switch (entry.Owner)
            {
                case DataOwner.Platform:
                    Assert.True(inPlatform && !inData, $"{type.Name} belongs to the platform model only");
                    break;
                case DataOwner.PlatformAndUser:
                    Assert.True(inPlatform && inData, $"{type.Name} belongs to both models");
                    break;
                default:
                    Assert.True(inData && !inPlatform, $"{type.Name} belongs to the data model only");
                    break;
            }
        }
    }

    [Fact]
    public void UserOwnedEntities_HaveAUserId()
    {
        foreach (var (type, entry) in DataOwnership.Map.Where(e => e.Value.Owner == DataOwner.User))
        {
            var userId = type.GetProperty("UserId");

            Assert.True(userId != null, $"{type.Name} has no UserId");
            Assert.Contains(userId!.PropertyType, new[] { typeof(Guid), typeof(Guid?) });
        }
    }

    [Fact]
    public void ParentOwnedEntities_PointAtAUserOwnedParent_ThroughARealForeignKey()
    {
        using var data = Data();

        foreach (var (type, entry) in DataOwnership.Map.Where(e => e.Value.Owner == DataOwner.Parent))
        {
            Assert.NotNull(entry.Parent);
            Assert.NotNull(entry.ParentKey);
            Assert.Equal(DataOwner.User, DataOwnership.Map[entry.Parent!].Owner);

            var foreignKey = data.Model.FindEntityType(type)!.GetForeignKeys()
                .SingleOrDefault(fk => fk.PrincipalEntityType.ClrType == entry.Parent && fk.Properties.Single().Name == entry.ParentKey);
            Assert.True(foreignKey != null, $"{type.Name}.{entry.ParentKey} is not a foreign key to {entry.Parent!.Name}");
        }
    }

    [Fact]
    public void NoDataEntity_PointsAtAPlatformEntity()
    {
        using var data = Data();

        var crossing = data.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => DataOwnership.Map[fk.PrincipalEntityType.ClrType].Owner == DataOwner.Platform)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name} -> {fk.PrincipalEntityType.ClrType.Name}");

        Assert.Empty(crossing);
    }
}
