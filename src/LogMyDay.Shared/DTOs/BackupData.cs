namespace LogMyDay.Shared.DTOs;

public class BackupData
{
    public BackupMetadata Metadata { get; set; } = new();
    public List<InputTypeBackup> InputTypes { get; set; } = new();
    public List<PatternBackup> Patterns { get; set; } = new();
    public List<UnitBackup> Units { get; set; } = new();
    public List<TagOptionListBackup> TagOptionLists { get; set; } = new();
    public List<TagOptionBackup> TagOptions { get; set; } = new();
    public List<TagBackup> Tags { get; set; } = new();
    public List<NotificationBackup> Notifications { get; set; } = new();
    public List<ActivityBackup> Activities { get; set; } = new();
}

public class BackupMetadata
{
    public DateTime ExportDate { get; set; }
    public string Version { get; set; } = "1.2";  // schema version of the backup file format
    public int TotalTags { get; set; }
    public int TotalActivities { get; set; }
    public int TotalInputTypes { get; set; }
    public int TotalPatterns { get; set; }
    public int TotalUnits { get; set; }
    public int TotalTagOptionLists { get; set; }
    public int TotalTagOptions { get; set; }
    public int TotalNotifications { get; set; }
}
