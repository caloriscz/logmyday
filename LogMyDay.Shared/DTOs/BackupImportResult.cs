namespace LogMyDay.Shared.DTOs;

public class BackupImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public BackupImportStatistics Statistics { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class BackupImportStatistics
{
    public int InputTypesImported { get; set; }
    public int InputTypesSkipped { get; set; }
    public int PatternsImported { get; set; }
    public int PatternsSkipped { get; set; }
    public int TagsImported { get; set; }
    public int TagsSkipped { get; set; }
    public int ActivitiesImported { get; set; }
    public int ActivitiesSkipped { get; set; }
    public int RecordsCleared { get; set; }
}

public class BackupValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
