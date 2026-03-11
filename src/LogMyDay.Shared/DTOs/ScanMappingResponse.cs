using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class ScanMappingResponse
{
    public int Id { get; set; }
    public string CodeValue { get; set; } = string.Empty;
    public CodeType CodeType { get; set; }
    public int TagId { get; set; }
    public string? TagName { get; set; }
    public string? DisplayName { get; set; }
    public string? DefaultDescription { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}
