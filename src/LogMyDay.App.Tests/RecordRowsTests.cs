using System.Globalization;
using LogMyDay.Domain.Constants;
using LogMyDay.Shared.DTOs;
using LogMyDay.UI.Formatting;

namespace LogMyDay.App.Tests;

public class RecordRowsTests
{
    private static readonly RecordTypeResponse BloodPressure = new()
    {
        Id = 1,
        Name = "Blood pressure",
        GroupName = "Health",
        Fields =
        [
            new RecordFieldResponse { TagId = 10, Name = "Systolic", TagName = "Blood pressure: Systolic", InputTypeId = InputTypeIds.Integer, Order = 0 },
            new RecordFieldResponse { TagId = 11, Name = "Diastolic", TagName = "Blood pressure: Diastolic", InputTypeId = InputTypeIds.Integer, Order = 1 },
            new RecordFieldResponse { TagId = 12, Name = "Pulse", TagName = "Blood pressure: Pulse", InputTypeId = InputTypeIds.Decimal, UnitSymbol = "bpm", Order = 2 },
            new RecordFieldResponse { TagId = 13, Name = "Rested", TagName = "Blood pressure: Rested", InputTypeId = InputTypeIds.Boolean, Order = 3 }
        ]
    };

    private static ActivityResponse Row(int id, int tagId, string value, int? recordId, int hour = 7) => new()
    {
        Id = id,
        PrimaryTagId = tagId,
        PrimaryTagName = $"Tag {tagId}",
        PrimaryTagValue = value,
        Description = value,
        RecordId = recordId,
        DateStarted = new DateTime(2026, 10, 11, hour, 10, 0)
    };

    [Fact]
    public void Collapse_ShowsARecordsValuesAsOneRow_InFieldOrder()
    {
        var fields = RecordRows.FieldIndex([BloodPressure]);
        var items = new List<ActivityResponse>
        {
            Row(1, 99, "plain", recordId: null, hour: 9),
            Row(5, 12, "64.50", recordId: 7),
            Row(3, 10, "128", recordId: 7),
            Row(4, 11, "82", recordId: 7),
            Row(6, 13, "true", recordId: 7),
            Row(8, 10, "120", recordId: 8, hour: 19)
        };

        var rows = RecordRows.Collapse(items, fields, CultureInfo.InvariantCulture);

        Assert.Equal(3, rows.Count);
        Assert.Equal("plain", rows[0].Description);
        var record = rows[1];
        Assert.Equal(7, record.RecordId);
        Assert.Equal(3, record.Id);
        Assert.Null(record.PrimaryTagId);
        Assert.Equal("Health: Blood pressure", record.PrimaryTagName);
        Assert.Equal("Systolic 128 · Diastolic 82 · Pulse 64.5 bpm · Rested yes", record.Description);
        Assert.Equal("Systolic 120", rows[2].Description);
    }

    [Fact]
    public void Collapse_LeavesValuesOfUnknownFieldsAsTheyAre()
    {
        var items = new List<ActivityResponse> { Row(1, 10, "128", recordId: 7), Row(2, 11, "82", recordId: 7) };

        Assert.Equal(2, RecordRows.Collapse(items, RecordRows.FieldIndex([]), CultureInfo.InvariantCulture).Count);
    }

    [Fact]
    public void FormatValue_ShowsAnOptionsDisplayName()
    {
        var position = new RecordFieldResponse { TagId = 14, Name = "Position", TagName = "Blood pressure: Position", InputTypeId = InputTypeIds.String, OptionListId = 3 };
        var lists = new Dictionary<int, TagOptionListResponse>
        {
            [3] = new() { Id = 3, Name = "Positions", Options = [new() { Id = 1, Value = "sit", DisplayName = "Sitting" }, new() { Id = 2, Value = "lie" }] }
        };

        Assert.Equal("Sitting", RecordRows.FormatValue(position, "sit", CultureInfo.InvariantCulture, lists));
        Assert.Equal("lie", RecordRows.FormatValue(position, "lie", CultureInfo.InvariantCulture, lists));
        Assert.Equal("sit", RecordRows.FormatValue(position, "sit", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void FormatValue_UsesTheDisplayCulture()
    {
        var pulse = BloodPressure.Fields[2];

        Assert.Equal("64,5 bpm", RecordRows.FormatValue(pulse, "64.50", CultureInfo.GetCultureInfo("cs-CZ")));
    }
}
