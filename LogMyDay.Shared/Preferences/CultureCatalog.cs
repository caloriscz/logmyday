using System;
using System.Collections.Generic;
using System.Globalization;

namespace LogMyDay.Shared.Preferences;

public static class CultureCatalog
{
    private static readonly Lazy<IReadOnlyList<SelectOption>> cultures = new(Build);

    public static IReadOnlyList<SelectOption> All => cultures.Value;

    private static IReadOnlyList<SelectOption> Build()
    {
        return CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .Select(culture => new SelectOption(
                culture.Name,
                $"{culture.NativeName} — {culture.Name}"))
            .OrderBy(option => option.Label, StringComparer.InvariantCultureIgnoreCase)
            .ToArray();
    }
}
