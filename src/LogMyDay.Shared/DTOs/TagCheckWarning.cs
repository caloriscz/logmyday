namespace LogMyDay.Shared.DTOs;

/// <summary>A Warn check that a save left broken on the saved day. The save went through; the
/// warning only tells what the day now contradicts.</summary>
public class TagCheckWarning
{
    public int CheckId { get; set; }
    public string CheckName { get; set; } = string.Empty;

    /// <summary>The check in plain words, e.g. "Fasting and Breakfast are not on the same day".</summary>
    public string Sentence { get; set; } = string.Empty;

    /// <summary>What the day holds for the check's two tags, e.g. "Fasting: yes · Breakfast: 1".</summary>
    public string Found { get; set; } = string.Empty;
}
