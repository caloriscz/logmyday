using LogMyDay.Domain.Constants;

namespace LogMyDay.Domain.Helpers;

/// <summary>
/// Built-in, direction-aware default coloring for scalar rating types. Used when a tag has no
/// assigned <see cref="Entities.ColorScheme"/>. Stars and Percentage read higher = better; Scores
/// read lower = better (e.g. pain / severity). Integer, Decimal and non-scalar types have no
/// meaningful built-in scale, so they return null and are colored only when a scheme is assigned.
/// </summary>
public static class DefaultColorSchemes
{
    public static string? Resolve(int inputTypeId, double value)
    {
        if (!TryGetScale(inputTypeId, out var min, out var max, out var higherIsBetter) || max <= min)
        {
            return null;
        }

        var ratio = Math.Clamp((value - min) / (max - min), 0.0, 1.0);
        var goodness = higherIsBetter ? ratio : 1.0 - ratio;

        return GradientColor(goodness);
    }

    public static bool HasDefault(int inputTypeId) => TryGetScale(inputTypeId, out _, out _, out _);

    private static bool TryGetScale(int inputTypeId, out double min, out double max, out bool higherIsBetter)
    {
        switch (inputTypeId)
        {
            case InputTypeIds.StarRating:
                min = 0; max = 5; higherIsBetter = true; return true;
            case InputTypeIds.StarRating10:
                min = 0; max = 10; higherIsBetter = true; return true;
            case InputTypeIds.Percentage:
                min = 0; max = 100; higherIsBetter = true; return true;
            case InputTypeIds.Score:
                min = 0; max = 5; higherIsBetter = false; return true;
            case InputTypeIds.Score10:
                min = 0; max = 10; higherIsBetter = false; return true;
            default:
                min = 0; max = 0; higherIsBetter = true; return false;
        }
    }

    // goodness: 0 = bad (red) → 0.5 = neutral (amber) → 1 = good (green).
    private static string GradientColor(double goodness)
    {
        // red #ef4444 → amber #eab308 → green #22c55e
        return goodness <= 0.5
            ? Lerp(0xef, 0x44, 0x44, 0xea, 0xb3, 0x08, goodness * 2)
            : Lerp(0xea, 0xb3, 0x08, 0x22, 0xc5, 0x5e, (goodness - 0.5) * 2);
    }

    private static string Lerp(int r1, int g1, int b1, int r2, int g2, int b2, double t)
    {
        var r = (int)(r1 + (r2 - r1) * t);
        var g = (int)(g1 + (g2 - g1) * t);
        var b = (int)(b1 + (b2 - b1) * t);

        return $"#{r:x2}{g:x2}{b:x2}";
    }
}
