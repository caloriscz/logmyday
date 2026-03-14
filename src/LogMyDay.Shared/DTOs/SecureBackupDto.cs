namespace LogMyDay.Shared.DTOs;

/// <summary>
/// Secure backup data structure that excludes user credentials and sensitive information
/// </summary>
public class SecureBackupDto
{
    public DateTime CreatedAt { get; set; }
    public string Version { get; set; } = "2.1";
    public List<SecureActivityBackupDto> Activities { get; set; } = new();
    public List<SecureTagBackupDto> Tags { get; set; } = new();
    public List<SecureTagGroupBackupDto> TagGroups { get; set; } = new();
    public List<SecureTagOptionListBackupDto> TagOptionLists { get; set; } = new();
    public List<SecureTagOptionBackupDto> TagOptions { get; set; } = new();
    public List<SecureNotificationBackupDto> Notifications { get; set; } = new();
    public List<SecureScanMappingBackupDto> ScanMappings { get; set; } = new();
}

/// <summary>
/// Activity backup data without user credentials - will be assigned to current user during restore
/// </summary>
public class SecureActivityBackupDto
{
    public int Id { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateStarted { get; set; }
    public DateTime? DateFinished { get; set; }
    public string TagName { get; set; } = string.Empty; // For restoration matching
}

/// <summary>
/// Tag backup data without user credentials - will be assigned to current user during restore
/// </summary>
public class SecureTagBackupDto
{
    public int Id { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? InputTypeName { get; set; }
    public bool IsRequired { get; set; }
    public string? TimeGranularity { get; set; }
    public bool IsRepeatable { get; set; }
    public bool IsRange { get; set; }
    public string? PatternName { get; set; }
    
    // Note: UserId not included - will be assigned to current user during restore
}

public class SecureTagGroupBackupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }
}

public class SecureTagOptionListBackupDto
{
    public string Name { get; set; } = string.Empty;
}

public class SecureTagOptionBackupDto
{
    public string Value { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string TagOptionListKey { get; set; } = string.Empty;
}

public class SecureNotificationBackupDto
{
    public string TagKey { get; set; } = string.Empty;
    public string? NotificationText { get; set; }
    public TimeSpan? NotBeforeTime { get; set; }
    public TimeSpan? NotAfterTime { get; set; }
    public int MaxNudges { get; set; }
    public TimeSpan? NudgeInterval { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}

public class SecureScanMappingBackupDto
{
    public string CodeValue { get; set; } = string.Empty;
    public int CodeType { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? DefaultDescription { get; set; }
    public bool IsActive { get; set; }
    public DateTime DateCreated { get; set; }
}

