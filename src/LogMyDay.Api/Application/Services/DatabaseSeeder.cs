using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Security;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly LogMyDayDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(LogMyDayDbContext context, IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Only auto-migrate for SQLite (development).
            // SQL Server (pre-prod/prod) migrations are applied manually via SQL scripts
            // in logmyday.wiki/migrations/.
            if (_context.Database.IsSqlite())
            {
                await _context.Database.MigrateAsync();
            }

            await EnsureDefaultUnitsAsync();

            // Check if any users exist
            var userCount = await _context.Users.CountAsync();
            if (userCount > 0)
            {
                _logger.LogInformation("Users already exist in database. Skipping admin user seeding.");
                return;
            }

            // Create the admin user with the same credentials as Basic Auth
            var adminUser = new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), // Same as Basic Auth UserId
                Email = "admin", // Convert username to email format
                DisplayName = "Administrator",
                PasswordHash = _passwordHasher.Hash("secret123"), // Same password as Basic Auth
                IsAdmin = true,
                Culture = PreferencesFactory.DefaultCulture,
                TimeZone = PreferencesFactory.DefaultTimeZoneId,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };

            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully seeded admin user: {Email} with ID: {UserId}",
                adminUser.Email, adminUser.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding database");
            throw;
        }
    }

    private async Task EnsureDefaultUnitsAsync()
    {
        if (await _context.Units.AnyAsync())
        {
            return;
        }

        _logger.LogInformation("Seeding default quantities and units");

        var timeQuantity = new Quantity { Key = "time" };
        var massQuantity = new Quantity { Key = "mass" };
        var countQuantity = new Quantity { Key = "count" };
        var volumeQuantity = new Quantity { Key = "volume" };
        var energyQuantity = new Quantity { Key = "energy" };
        var lengthQuantity = new Quantity { Key = "length" };
        var temperatureQuantity = new Quantity { Key = "temperature" };
        var dosageQuantity = new Quantity { Key = "dosage" };

        _context.Quantities.AddRange(
            timeQuantity, massQuantity, countQuantity,
            volumeQuantity, energyQuantity, lengthQuantity,
            temperatureQuantity, dosageQuantity);
        await _context.SaveChangesAsync();

        var second = new Unit
        {
            Key = "second",
            Symbol = "s",
            QuantityId = timeQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 0
        };

        var minute = new Unit
        {
            Key = "minute",
            Symbol = "min",
            QuantityId = timeQuantity.Id,
            AToBase = 60,
            BToBase = 0,
            Decimals = 0
        };

        var hour = new Unit
        {
            Key = "hour",
            Symbol = "h",
            QuantityId = timeQuantity.Id,
            AToBase = 3600,
            BToBase = 0,
            Decimals = 1
        };

        var kilogram = new Unit
        {
            Key = "kilogram",
            Symbol = "kg",
            QuantityId = massQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 2
        };

        var gram = new Unit
        {
            Key = "gram",
            Symbol = "g",
            QuantityId = massQuantity.Id,
            AToBase = 0.001,
            BToBase = 0,
            Decimals = 0
        };

        var milligram = new Unit
        {
            Key = "milligram",
            Symbol = "mg",
            QuantityId = massQuantity.Id,
            AToBase = 0.000001,
            BToBase = 0,
            Decimals = 0
        };

        var microgram = new Unit
        {
            Key = "microgram",
            Symbol = "µg",
            QuantityId = massQuantity.Id,
            AToBase = 0.000000001,
            BToBase = 0,
            Decimals = 0
        };

        var count = new Unit
        {
            Key = "count",
            Symbol = "ct",
            QuantityId = countQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 0
        };

        var liter = new Unit
        {
            Key = "liter",
            Symbol = "L",
            QuantityId = volumeQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 2
        };

        var centiliter = new Unit
        {
            Key = "centiliter",
            Symbol = "cL",
            QuantityId = volumeQuantity.Id,
            AToBase = 0.01,
            BToBase = 0,
            Decimals = 0
        };

        var milliliter = new Unit
        {
            Key = "milliliter",
            Symbol = "mL",
            QuantityId = volumeQuantity.Id,
            AToBase = 0.001,
            BToBase = 0,
            Decimals = 0
        };

        var kilocalorie = new Unit
        {
            Key = "kilocalorie",
            Symbol = "kcal",
            QuantityId = energyQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 0
        };

        var calorie = new Unit
        {
            Key = "calorie",
            Symbol = "cal",
            QuantityId = energyQuantity.Id,
            AToBase = 0.001,
            BToBase = 0,
            Decimals = 0
        };

        var kilojoule = new Unit
        {
            Key = "kilojoule",
            Symbol = "kJ",
            QuantityId = energyQuantity.Id,
            AToBase = 0.239006,
            BToBase = 0,
            Decimals = 0
        };

        var meter = new Unit
        {
            Key = "meter",
            Symbol = "m",
            QuantityId = lengthQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 2
        };

        var centimeter = new Unit
        {
            Key = "centimeter",
            Symbol = "cm",
            QuantityId = lengthQuantity.Id,
            AToBase = 0.01,
            BToBase = 0,
            Decimals = 0
        };

        var kilometer = new Unit
        {
            Key = "kilometer",
            Symbol = "km",
            QuantityId = lengthQuantity.Id,
            AToBase = 1000,
            BToBase = 0,
            Decimals = 3
        };

        var celsius = new Unit
        {
            Key = "celsius",
            Symbol = "°C",
            QuantityId = temperatureQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 1
        };

        var fahrenheit = new Unit
        {
            Key = "fahrenheit",
            Symbol = "°F",
            QuantityId = temperatureQuantity.Id,
            AToBase = 0.555556,
            BToBase = -17.777778,
            Decimals = 1
        };

        var iu = new Unit
        {
            Key = "iu",
            Symbol = "IU",
            QuantityId = dosageQuantity.Id,
            AToBase = 1,
            BToBase = 0,
            Decimals = 0
        };

        _context.Units.AddRange(
            second, minute, hour,
            kilogram, gram, milligram, microgram,
            count,
            liter, centiliter, milliliter,
            kilocalorie, calorie, kilojoule,
            meter, centimeter, kilometer,
            celsius, fahrenheit,
            iu);
        await _context.SaveChangesAsync();

        timeQuantity.BaseUnitId = second.Id;
        massQuantity.BaseUnitId = kilogram.Id;
        countQuantity.BaseUnitId = count.Id;
        volumeQuantity.BaseUnitId = liter.Id;
        energyQuantity.BaseUnitId = kilocalorie.Id;
        lengthQuantity.BaseUnitId = meter.Id;
        temperatureQuantity.BaseUnitId = celsius.Id;
        dosageQuantity.BaseUnitId = iu.Id;

        await _context.SaveChangesAsync();
    }
}
