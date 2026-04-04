namespace LogMyDay.Shared.DTOs;

public class PeriodSumResponse
{
    public double CurrentSum { get; set; }
    public double? MaxValue { get; set; }
    public double? RemainingCapacity { get; set; }
    public double? ExistingValue { get; set; }
    public int? ExistingActivityId { get; set; }
    public bool IsNonRepeatableNumeric { get; set; }
}
