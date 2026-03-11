namespace LogMyDay.Shared.DTOs;

public class ScanLookupResponse
{
    public bool Found { get; set; }
    public ScanMappingResponse? Mapping { get; set; }
    public TagResponse? Tag { get; set; }
}
