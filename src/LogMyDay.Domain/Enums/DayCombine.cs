namespace LogMyDay.Domain.Enums;

/// <summary>How a record field's values on one day become the day's value, where a view or a Tag
/// Activity Relations rule needs one value per day. Sum and average apply to number fields; any
/// other field uses the latest value.</summary>
public enum DayCombine
{
    Sum = 1,
    Average = 2,
    Latest = 3
}
