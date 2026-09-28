using System.Globalization;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.UI.Formatting;

/// <summary>Plain-language descriptions of Tag Activity Relations rules, shared by the web and
/// mobile Rules pages and the rule editor.</summary>
public static class TagRuleText
{
    /// <summary>The right-hand side of "Target = …", e.g. "2 × A + 0.5 × B" or "average of A, B".</summary>
    public static string Formula(TagRuleResponse rule, CultureInfo culture)
    {
        var sources = rule.Sources.Select(s => (s.SourceTagName, s.Factor)).ToList();

        return Formula(rule.Template, rule.AggregateKind, sources, culture);
    }

    public static string Formula(string template, string? aggregateKind, IReadOnlyList<(string Name, double Factor)> sources, CultureInfo culture)
    {
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
}
