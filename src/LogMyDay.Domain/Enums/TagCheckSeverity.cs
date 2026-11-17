namespace LogMyDay.Domain.Enums;

/// <summary>How a broken check is reported.</summary>
public enum TagCheckSeverity
{
    /// <summary>Shown in the Rule review only.</summary>
    Flag = 0,

    /// <summary>Shown in the Rule review and right after a save that breaks it; the save goes
    /// through.</summary>
    Warn = 1
}
