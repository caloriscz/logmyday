using LogMyDay.Domain.Constants;

namespace LogMyDay.Api.Application.Services.Widgets;

public record WidgetParameterDefinition(
    string Key,
    string Label,
    bool IsRequired,
    ParameterValueType ValueType,
    ParameterInputType InputType,
    string? Description = null,
    IReadOnlyList<string>? Options = null
);
