namespace LogMyDay.Domain.Enums;

/// <summary>When a trigger's reminder rings.</summary>
public enum TagTriggerTiming
{
    /// <summary>A number of minutes after the entry of the first tag that started it.</summary>
    AfterEntry = 0,

    /// <summary>At a time of day: the first one after that entry, that day or the next.</summary>
    AtTime = 1
}
