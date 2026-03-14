namespace LogMyDay.Shared.DTOs;

public class ScanMappingBackup
{
    public string CodeValue { get; set; } = string.Empty;
    public int CodeType { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DefaultDescription { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}
