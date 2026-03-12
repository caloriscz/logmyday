using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class ScanMappingRequest
{
    public required string CodeValue { get; set; }
    public CodeType CodeType { get; set; }
    public int TagId { get; set; }
    public string? DisplayName { get; set; }
    public string? DefaultDescription { get; set; }
    public bool IsActive { get; set; } = true;
}
