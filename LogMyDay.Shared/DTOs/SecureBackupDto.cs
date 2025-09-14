namespace LogMyDay.Shared.DTOs;

/// <summary>
/// Secure backup data structure that excludes user credentials and sensitive information
/// </summary>
public class SecureBackupDto
{
    public DateTime CreatedAt { get; set; }
    public string Version { get; set; } = "2.0";
    public List<SecureActivityBackupDto> Activities { get; set; } = new();
    public List<SecureTagBackupDto> Tags { get; set; } = new();
    
    // Note: Explicitly NO user data, credentials, or sensitive information
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
    
    // Note: UserId not included - will be assigned to current user during restore
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

