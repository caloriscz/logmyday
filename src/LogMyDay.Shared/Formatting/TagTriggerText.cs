using LogMyDay.Shared.DTOs;

namespace LogMyDay.Shared.Formatting;

/// <summary>Plain-language descriptions of triggers, shared by the web and mobile pages, the
/// trigger editor and the server.</summary>
public static class TagTriggerText
{
    /// <summary>"When Gabanox is logged, remind me to log Effect 3 h later" or "When Alcohol is
    /// yes, remind me to log Sleep quality at 08:00".</summary>
    public static string Sentence(TagTriggerResponse trigger) =>
        Sentence(trigger.IfTagName, trigger.IfOperator, trigger.IfOperand, trigger.PromptTagName,
            trigger.Timing, trigger.OffsetMinutes, trigger.AtTime);

    public static string Sentence(string ifTag, string ifOperator, string? ifOperand, string promptTag,
        string timing, int? offsetMinutes, TimeOnly? atTime)
    {
        return $"When {TagCheckText.Condition(ifTag, ifOperator, ifOperand)}, remind me to log {promptTag} {When(timing, offsetMinutes, atTime)}";
    }

    /// <summary>"3 h later", "1 h 30 min later" or "at 08:00".</summary>
    public static string When(string timing, int? offsetMinutes, TimeOnly? atTime)
    {
        if (timing == "AtTime")
        {
            return atTime is TimeOnly time ? $"at {time:HH\\:mm}" : "at a time";
        }

        return offsetMinutes is int minutes ? $"{Duration(minutes)} later" : "later";
    }

    /// <summary>"45 min", "3 h", "1 h 30 min".</summary>
    public static string Duration(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;

        return (hours, rest) switch
        {
            (0, _) => $"{rest} min",
            (_, 0) => $"{hours} h",
            _ => $"{hours} h {rest} min"
        };
    }
}
