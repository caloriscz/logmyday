using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Formatting;

/// <summary>Plain-language descriptions of checks and of what the Rule review found, shared by
/// the web and mobile pages and the check editor.</summary>
public static class TagCheckText
{
    /// <summary>"Fasting and Breakfast are not on the same day", "Shake requires Exercise on the
    /// same day" or "If Coffees = 4, then Late coffee should be no".</summary>
    public static string Sentence(TagCheckResponse check) =>
        Sentence(check.IfTagName, check.IfOperator, check.IfOperand, check.ThenTagName, check.ThenOperator, check.ThenOperand);

    public static string Sentence(string ifTag, string ifOperator, string? ifOperand, string thenTag, string thenOperator, string? thenOperand)
    {
        if (ifOperator == "IsLogged" && thenOperator == "IsNotLogged")
        {
            return $"{ifTag} and {thenTag} are not on the same day";
        }

        if (ifOperator == "IsLogged" && thenOperator == "IsLogged")
        {
            return $"{ifTag} requires {thenTag} on the same day";
        }

        return $"If {Condition(ifTag, ifOperator, ifOperand)}, then {Expectation(thenTag, thenOperator, thenOperand)}";
    }

    /// <summary>What the review found on the day, e.g. "Coffees: 4 · Late coffee: yes".</summary>
    public static string Found(TagCheckReviewItem item)
    {
        var then = item.ThenValue != null
            ? Value(item.ThenValue)
            : item.IsPending ? "not logged yet" : "not logged";

        return $"{item.IfTagName}: {Value(item.IfValue ?? "logged")} · {item.ThenTagName}: {then}";
    }

    /// <summary>The label of a test in the check editor.</summary>
    public static string OperatorLabel(string op, bool numeric) => op switch
    {
        "IsLogged" => "is logged",
        "IsNotLogged" => "is not logged",
        "IsYes" => "is yes",
        "IsNo" => "is no",
        "Equal" => numeric ? "=" : "equals",
        "NotEqual" => numeric ? "≠" : "does not equal",
        _ => TagRuleText.Symbol(op)
    };

    private static string Condition(string tag, string op, string? operand) => op switch
    {
        "IsLogged" => $"{tag} is logged",
        "IsYes" => $"{tag} is yes",
        "IsNo" => $"{tag} is no",
        _ => $"{tag} {TagRuleText.Symbol(op)} {operand}"
    };

    private static string Expectation(string tag, string op, string? operand) => op switch
    {
        "IsLogged" => $"{tag} should be logged",
        "IsNotLogged" => $"{tag} should not be logged",
        "IsYes" => $"{tag} should be yes",
        "IsNo" => $"{tag} should be no",
        _ => $"{tag} should be {TagRuleText.Symbol(op)} {operand}"
    };

    // Yes/no values are stored as true/false, older rows and some edits as True/False; people
    // read them as yes/no.
    private static string Value(string value) => value.ToLowerInvariant() switch
    {
        "true" => "yes",
        "false" => "no",
        _ => value
    };
}
