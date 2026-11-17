namespace LogMyDay.Shared.DTOs;

public class TagCheckResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>IfThen, NotOnTheSameDay (If is logged ⇒ Then is not logged) or Requires (If is
    /// logged ⇒ Then is logged).</summary>
    public string Kind { get; set; } = "IfThen";

    /// <summary>Flag or Warn.</summary>
    public string Severity { get; set; } = "Flag";

    public bool IsEnabled { get; set; }

    public int IfTagId { get; set; }
    public string IfTagName { get; set; } = string.Empty;

    /// <summary>IsLogged, Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual, IsYes or IsNo.</summary>
    public string IfOperator { get; set; } = string.Empty;

    public string? IfOperand { get; set; }

    public int ThenTagId { get; set; }
    public string ThenTagName { get; set; } = string.Empty;

    /// <summary>As <see cref="IfOperator"/>, plus IsNotLogged.</summary>
    public string ThenOperator { get; set; } = string.Empty;

    public string? ThenOperand { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }
}
