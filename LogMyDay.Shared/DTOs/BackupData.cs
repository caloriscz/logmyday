namespace LogMyDay.Shared.DTOs;

public class BackupData
{
    public BackupMetadata Metadata { get; set; } = new();
    public List<InputTypeBackup> InputTypes { get; set; } = new();
    public List<PatternBackup> Patterns { get; set; } = new();
    public List<TagBackup> Tags { get; set; } = new();
    public List<ActivityBackup> Activities { get; set; } = new();
}

public class BackupMetadata
{
    public DateTime ExportDate { get; set; }
    public string Version { get; set; } = "1.0";
    public int TotalTags { get; set; }
    public int TotalActivities { get; set; }
    public int TotalInputTypes { get; set; }
    public int TotalPatterns { get; set; }
}
