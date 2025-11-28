using System.Globalization;

namespace LogMyDay.Shared.Preferences;

public sealed record EffectivePreferences(
    DayOfWeek StartOfWeek,
    CalendarWeekRule WeekRule,
    bool Use24HourClock,
    string ShortDatePattern,
    string LongDatePattern,
    string ShortTimePattern,
    string LongTimePattern,
    string DecimalSeparator,
    string ThousandSeparator,
    int[] NumberGroupSizes,
    string ListSeparator,
    bool IsMetric,
    string CurrencySymbol,
    string IsoCurrency,
    string Culture,
    string TimeZoneId);

public static class PreferencesFactory
{
    public const string DefaultCulture = "en-US";
    public const string DefaultTimeZoneId = "Europe/Vienna";

    public static EffectivePreferences From(string cultureName, string timeZoneId)
    {
        var normalizedCulture = NormalizeCulture(cultureName);
        var normalizedTimeZone = NormalizeTimeZone(timeZoneId);

        var culture = CultureInfo.GetCultureInfo(normalizedCulture);
        var dateTime = culture.DateTimeFormat;
        var number = culture.NumberFormat;
        var region = new RegionInfo(culture.LCID);

        return new EffectivePreferences(
            StartOfWeek: dateTime.FirstDayOfWeek,
            WeekRule: dateTime.CalendarWeekRule,
            Use24HourClock: dateTime.ShortTimePattern.Contains('H'),
            ShortDatePattern: dateTime.ShortDatePattern,
            LongDatePattern: dateTime.LongDatePattern,
            ShortTimePattern: dateTime.ShortTimePattern,
            LongTimePattern: dateTime.LongTimePattern,
            DecimalSeparator: number.NumberDecimalSeparator,
            ThousandSeparator: number.NumberGroupSeparator,
            NumberGroupSizes: number.NumberGroupSizes.ToArray(),
            ListSeparator: culture.TextInfo.ListSeparator,
            IsMetric: region.IsMetric,
            CurrencySymbol: region.CurrencySymbol,
            IsoCurrency: region.ISOCurrencySymbol,
            Culture: culture.Name,
            TimeZoneId: normalizedTimeZone);
    }

    public static string NormalizeCulture(string? cultureName)
    {
        var value = string.IsNullOrWhiteSpace(cultureName) ? DefaultCulture : cultureName.Trim();

        return CultureInfo.GetCultureInfo(value).Name;
    }

    public static string NormalizeTimeZone(string? timeZoneId)
    {
        var value = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(value);

        return timeZone.Id;
    }
}
