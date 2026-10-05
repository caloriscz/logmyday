using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Constants;

/// <summary>Which tags each Tag Activity Relations template accepts, in one place for the rule
/// validation, backup import, the engine and the editor.</summary>
public static class TagRuleInputTypes
{
    /// <summary>Integer or decimal: weighted-sum sources and number targets.</summary>
    public static bool IsNumber(int? inputTypeId) => inputTypeId is InputTypeIds.Integer or InputTypeIds.Decimal;

    /// <summary>Rating, score or percentage: bounded scales.</summary>
    public static bool IsScale(int? inputTypeId) => inputTypeId is InputTypeIds.StarRating or InputTypeIds.StarRating10
        or InputTypeIds.Percentage or InputTypeIds.Score or InputTypeIds.Score10;

    /// <summary>Numbers and scales: aggregate (average, minimum, maximum) sources.</summary>
    public static bool IsNumeric(int? inputTypeId) => IsNumber(inputTypeId) || IsScale(inputTypeId);

    /// <summary>A conditional result: text, yes/no, a number or one option of a list.</summary>
    public static bool IsConditionalTarget(int? inputTypeId, bool hasOptionList) =>
        hasOptionList || inputTypeId is InputTypeIds.Integer or InputTypeIds.String or InputTypeIds.Boolean or InputTypeIds.Decimal;

    /// <summary>Whether a tag can be a source of the template. Count takes any tag.</summary>
    public static bool FitsSource(TagRuleTemplate template, TagRuleAggregateKind? aggregateKind, int? inputTypeId) => template switch
    {
        TagRuleTemplate.WeightedSum => IsNumber(inputTypeId),
        TagRuleTemplate.Aggregate when aggregateKind == TagRuleAggregateKind.Count => true,
        TagRuleTemplate.Aggregate => IsNumeric(inputTypeId),
        TagRuleTemplate.Conditional => true,
        _ => false
    };

    /// <summary>Whether a tag can hold the template's results.</summary>
    public static bool FitsTarget(TagRuleTemplate template, int? inputTypeId, bool hasOptionList) =>
        template == TagRuleTemplate.Conditional ? IsConditionalTarget(inputTypeId, hasOptionList) : IsNumber(inputTypeId);

    /// <summary>How a conditional rule reads a source tag. An option list makes any tag a choice
    /// between options, compared as text.</summary>
    public static TagRuleValueKind ConditionKind(int? inputTypeId, bool hasOptionList)
    {
        if (hasOptionList)
        {
            return TagRuleValueKind.Text;
        }

        if (IsNumber(inputTypeId))
        {
            return TagRuleValueKind.Number;
        }

        if (IsScale(inputTypeId))
        {
            return TagRuleValueKind.Scale;
        }

        return inputTypeId == InputTypeIds.Boolean ? TagRuleValueKind.YesNo : TagRuleValueKind.Text;
    }

    /// <summary>The tests a conditional case may use on a source of this kind.</summary>
    public static bool Allows(TagRuleValueKind kind, TagRuleOperator op) => op switch
    {
        TagRuleOperator.IsLogged or TagRuleOperator.Otherwise => true,
        TagRuleOperator.IsYes or TagRuleOperator.IsNo => kind == TagRuleValueKind.YesNo,
        TagRuleOperator.Equal or TagRuleOperator.NotEqual => kind != TagRuleValueKind.YesNo,
        TagRuleOperator.Greater or TagRuleOperator.GreaterOrEqual or TagRuleOperator.Less or TagRuleOperator.LessOrEqual =>
            kind is TagRuleValueKind.Number or TagRuleValueKind.Scale,
        _ => false
    };

    /// <summary>Whether the case compares with an operand.</summary>
    public static bool NeedsOperand(TagRuleOperator op) =>
        op is TagRuleOperator.Equal or TagRuleOperator.NotEqual or TagRuleOperator.Greater
            or TagRuleOperator.GreaterOrEqual or TagRuleOperator.Less or TagRuleOperator.LessOrEqual;
}
