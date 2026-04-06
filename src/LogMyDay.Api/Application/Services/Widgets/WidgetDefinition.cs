namespace LogMyDay.Api.Application.Services.Widgets;

public record WidgetDefinition(
    int WidgetTypeId,
    string Name,
    string Description,
    IReadOnlyList<WidgetParameterDefinition> Parameters
)
{
    public bool UsesTag => Parameters.Any(p => p.ValueType == Domain.Constants.ParameterValueType.Tag);
}
