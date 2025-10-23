namespace LogMyDay.Shared.DTOs;

public class TagOptionBackup
{
    public string Value { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string TagOptionListKey { get; set; } = string.Empty; // Reference to TagOptionListBackup.Name
}
