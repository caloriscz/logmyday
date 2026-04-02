using LogMyDay.Domain.Constants;

namespace LogMyDay.Domain.Helpers;

public record InputTypeConstraints(
    double? MinValue,
    double? MaxValue,
    double? Step,
    bool IsRangeApplicable
);

public static class InputTypeDefaults
{
    public static InputTypeConstraints GetConstraintsForType(int inputTypeId)
    {
        return inputTypeId switch
        {
            InputTypeIds.Integer => new InputTypeConstraints(
                MinValue: null,
                MaxValue: null,
                Step: 1,
                IsRangeApplicable: true
            ),
            InputTypeIds.String => new InputTypeConstraints(
                MinValue: null,
                MaxValue: null,
                Step: null,
                IsRangeApplicable: false
            ),
            InputTypeIds.Boolean => new InputTypeConstraints(
                MinValue: null,
                MaxValue: null,
                Step: null,
                IsRangeApplicable: false
            ),
            InputTypeIds.Date => new InputTypeConstraints(
                MinValue: null,
                MaxValue: null,
                Step: null,
                IsRangeApplicable: false
            ),
            InputTypeIds.Time => new InputTypeConstraints(
                MinValue: null,
                MaxValue: null,
                Step: null,
                IsRangeApplicable: false
            ),
            InputTypeIds.Decimal => new InputTypeConstraints(
                MinValue: null,
                MaxValue: null,
                Step: 0.01,
                IsRangeApplicable: true
            ),
            InputTypeIds.StarRating => new InputTypeConstraints(
                MinValue: 0,
                MaxValue: 5,
                Step: 1,
                IsRangeApplicable: true
            ),
            InputTypeIds.StarRating10 => new InputTypeConstraints(
                MinValue: 0,
                MaxValue: 10,
                Step: 1,
                IsRangeApplicable: true
            ),
            InputTypeIds.Percentage => new InputTypeConstraints(
                MinValue: 0,
                MaxValue: 100,
                Step: 1,
                IsRangeApplicable: true
            ),
            InputTypeIds.Score => new InputTypeConstraints(
                MinValue: 0,
                MaxValue: 5,
                Step: 1,
                IsRangeApplicable: true
            ),
            InputTypeIds.Score10 => new InputTypeConstraints(
                MinValue: 0,
                MaxValue: 10,
                Step: 1,
                IsRangeApplicable: true
            ),
            _ => new InputTypeConstraints(
                MinValue: null,
                MaxValue: null,
                Step: null,
                IsRangeApplicable: false
            )
        };
    }
}
