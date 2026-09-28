using System.Globalization;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Formatting;

/// <summary>Plain-language descriptions of Tag Activity Relations rules, shared by the web and
/// mobile Rules pages and the rule editor.</summary>
public static class TagRuleText
{
    /// <summary>A conditional case as text: operator name, compared value and result.</summary>
    public readonly record struct Case(string Operator, string? Operand, string Result);

    /// <summary>The right-hand side of "Target = …", e.g. "2 × A + 0.5 × B", "average of A, B" or
    /// "'Over limit' if Coffees ≥ 4; otherwise 'Low'".</summary>
    public static string Formula(TagRuleResponse rule, CultureInfo culture)
    {
        var sources = rule.Sources.Select(s => (s.SourceTagName, s.Factor)).ToList();
        var cases = rule.Cases.Select(c => new Case(c.Operator, c.Operand, c.ResultValue)).ToList();

        return Formula(rule.Template, rule.AggregateKind, sources, cases, culture);
    }

    public static string Formula(string template, string? aggregateKind, IReadOnlyList<(string Name, double Factor)> sources,
        IReadOnlyList<Case> cases, CultureInfo culture)
    {
        if (template == "Conditional")
        {
            var source = sources.FirstOrDefault().Name ?? "the source";

            return string.Join("; ", cases.Select(c => c.Operator switch
            {
                "IsLogged" => $"{Quote(c.Result)} when {source} is logged",
                "Otherwise" => $"otherwise {Quote(c.Result)}",
                "IsYes" => $"{Quote(c.Result)} if {source} is yes",
                "IsNo" => $"{Quote(c.Result)} if {source} is no",
                _ => $"{Quote(c.Result)} if {source} {Symbol(c.Operator)} {c.Operand}"
            }));
        }

        if (template == "Aggregate")
        {
            var names = string.Join(", ", sources.Select(s => s.Name));

            return aggregateKind switch
            {
                "Minimum" => $"minimum of {names}",
                "Maximum" => $"maximum of {names}",
                "Count" => $"number of entries of {names}",
                _ => $"average of {names}"
            };
        }

        return string.Join(" + ", sources.Select(s => $"{s.Factor.ToString("0.####", culture)} × {s.Name}"));
    }

    /// <summary>The comparison symbol for a conditional operator name.</summary>
    public static string Symbol(string op) => op switch
    {
        "Equal" => "=",
        "NotEqual" => "≠",
        "Greater" => ">",
        "GreaterOrEqual" => "≥",
        "Less" => "<",
        "LessOrEqual" => "≤",
        _ => op
    };

    // Yes/no results are stored as true/false; people read them as yes/no.
    private static string Quote(string result) => result switch
    {
        "true" => "yes",
        "false" => "no",
        _ => $"'{result}'"
    };
}
