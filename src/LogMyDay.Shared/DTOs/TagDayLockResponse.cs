using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TagDayLockResponse
{
    public int Id { get; set; }
    public int TagId { get; set; }
    public string? TagName { get; set; }
    public DateOnly Date { get; set; }
    public bool IsLocked { get; set; }
    public DateTime SetAt { get; set; }
    public DayLockSetBy SetBy { get; set; }
    public string? Reason { get; set; }
}
