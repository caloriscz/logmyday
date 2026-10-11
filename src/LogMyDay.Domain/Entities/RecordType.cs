namespace LogMyDay.Domain.Entities;

/// <summary>The definition of a record: several values measured together at one moment, e.g.
/// "Blood pressure" with systolic, diastolic and pulse. Each field is a tag of its own that belongs
/// to this type alone (<see cref="Tag.RecordTypeId"/>).</summary>
public class RecordType
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }

    /// <summary>The Tag Group the type and its fields are listed in.</summary>
    public int? GroupId { get; set; }
    public TagGroup? Group { get; set; }

    /// <summary>At most one record of this type per local day.</summary>
    public bool OncePerDay { get; set; }

    public int DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }

    public ICollection<Tag> Fields { get; set; } = new List<Tag>();
}
