namespace LogMyDay.Domain.Entities;

/// <summary>One measurement of a record type (a tag with <see cref="Tag.IsRecordType"/>) at one
/// point in time. Its values are ordinary activities on the type's field tags that point here
/// (<see cref="Activity.RecordId"/>) and carry the record's time.</summary>
public class Record
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    public int RecordTypeId { get; set; }
    public Tag? RecordType { get; set; }

    public DateTime DateStarted { get; set; }
    public DateTime DateCreated { get; set; }

    public ICollection<Activity> Values { get; set; } = new List<Activity>();
}
