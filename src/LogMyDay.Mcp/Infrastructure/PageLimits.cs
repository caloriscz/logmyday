namespace LogMyDay.Mcp.Infrastructure;

/// <summary>List tools clamp rather than reject an oversized page, and echo the size they used.</summary>
public static class PageLimits
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static int Clamp(int? requested)
    {
        var size = requested ?? DefaultPageSize;

        return Math.Clamp(size, 1, MaxPageSize);
    }

    public static int Page(int? requested) => Math.Max(1, requested ?? 1);
}
