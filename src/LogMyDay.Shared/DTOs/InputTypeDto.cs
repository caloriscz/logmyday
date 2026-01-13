namespace LogMyDay.Shared.DTOs;

public class InputTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRangeEditable { get; set; } = true;
    public bool IsMinimumEditable { get; set; } = true;
    public bool IsMaximumEditable { get; set; } = true;
    public bool IsStepEditable { get; set; } = true;
    public bool IsRepeatableEditable { get; set; } = true;
    public string? Description { get; set; }
}
