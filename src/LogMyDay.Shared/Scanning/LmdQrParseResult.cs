namespace LogMyDay.Shared.Scanning;

public class LmdQrParseResult
{
    public bool IsAppFormatted { get; init; }
    public int? TagId { get; init; }
    public string? Value { get; init; }
    public string? DisplayName { get; init; }

    public static LmdQrParseResult NotAppFormatted => new() { IsAppFormatted = false };
}
