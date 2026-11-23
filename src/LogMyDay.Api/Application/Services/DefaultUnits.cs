using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Application.Services;

/// <summary>The quantities and units every user's database starts with.</summary>
public static class DefaultUnits
{
    /// <summary>
    /// Adds the default quantities and units to a database that has no units yet. Synchronous, as
    /// its caller is: UserDatabases.Ensure prepares a user's database while a data context is
    /// being created, with a short-lived context of its own.
    /// </summary>
    public static void Ensure(LogMyDayDbContext context)
    {
        if (context.Units.Any())
        {
            return;
        }

        // One transaction for the three saves: a seed that is interrupted leaves nothing behind,
        // so the next attempt seeds again, instead of finding units without base units or adding
        // the quantities a second time.
        using var transaction = context.Database.BeginTransaction();

        var timeQuantity = new Quantity { Key = "time" };
        var massQuantity = new Quantity { Key = "mass" };
        var countQuantity = new Quantity { Key = "count" };
        var volumeQuantity = new Quantity { Key = "volume" };
        var energyQuantity = new Quantity { Key = "energy" };
        var lengthQuantity = new Quantity { Key = "length" };
        var temperatureQuantity = new Quantity { Key = "temperature" };
        var dosageQuantity = new Quantity { Key = "dosage" };

        context.Quantities.AddRange(
            timeQuantity, massQuantity, countQuantity,
            volumeQuantity, energyQuantity, lengthQuantity,
            temperatureQuantity, dosageQuantity);
        context.SaveChanges();

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

        context.Units.AddRange(
            second, minute, hour,
            kilogram, gram, milligram, microgram,
            count,
            liter, centiliter, milliliter,
            kilocalorie, calorie, kilojoule,
            meter, centimeter, kilometer,
            celsius, fahrenheit,
            iu);
        context.SaveChanges();

        timeQuantity.BaseUnitId = second.Id;
        massQuantity.BaseUnitId = kilogram.Id;
        countQuantity.BaseUnitId = count.Id;
        volumeQuantity.BaseUnitId = liter.Id;
        energyQuantity.BaseUnitId = kilocalorie.Id;
        lengthQuantity.BaseUnitId = meter.Id;
        temperatureQuantity.BaseUnitId = celsius.Id;
        dosageQuantity.BaseUnitId = iu.Id;

        context.SaveChanges();
        transaction.Commit();
    }
}
