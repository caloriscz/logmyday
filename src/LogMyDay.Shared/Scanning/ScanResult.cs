using LogMyDay.Shared.DTOs;

namespace LogMyDay.Shared.Scanning;

public class ScanResult
{
    public ScanResultType Type { get; init; }
    public int? TagId { get; init; }
    public TagResponse? Tag { get; init; }
    public string? PrefilledValue { get; init; }
    public string? DisplayName { get; init; }
    public ScanMappingResponse? Mapping { get; init; }
    public string? ScannedValue { get; init; }
}

public enum ScanResultType
{
    TagFound,
    TagNotFound,
    UnknownCode,
    Error
}
