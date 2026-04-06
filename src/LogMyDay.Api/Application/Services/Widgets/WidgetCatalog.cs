using LogMyDay.Domain.Constants;

namespace LogMyDay.Api.Application.Services.Widgets;

public static class WidgetCatalog
{
    private static readonly WidgetParameterDefinition TagParameter = new(
        Key: "tagId",
        Label: "Tag",
        IsRequired: true,
        ValueType: ParameterValueType.Tag,
        InputType: ParameterInputType.TagSelect
    );

    public static readonly IReadOnlyList<WidgetDefinition> All = new List<WidgetDefinition>
    {
        new(
            WidgetTypeId: WidgetTypeIds.LatestValue,
            Name: "Latest Value",
            Description: "Shows the most recent recorded value for a tag today.",
            Parameters: new[] { TagParameter }
        ),
        new(
            WidgetTypeId: WidgetTypeIds.WeeklyAverage,
            Name: "Weekly Average",
            Description: "Shows the average of the latest recorded value per day over the last 7 days.",
            Parameters: new[] { TagParameter }
        ),
        new(
            WidgetTypeId: WidgetTypeIds.MonthlyMinMax,
            Name: "Monthly Min / Max",
            Description: "Shows the minimum and maximum recorded value over the last 30 days.",
            Parameters: new[] { TagParameter }
        )
    };

    public static WidgetDefinition? Get(int widgetTypeId) =>
        All.FirstOrDefault(w => w.WidgetTypeId == widgetTypeId);
}
