using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class TagRuleEngine : ITagRuleEngine
{
    private readonly LogMyDayDbContext _context;

    public TagRuleEngine(LogMyDayDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task OnSourcesChanged(Guid userId, IReadOnlyCollection<(int TagId, DateOnly Day)> changes)
    {
        if (changes.Count == 0)
        {
            return;
        }

        var tagIds = changes.Select(c => c.TagId).Distinct().ToList();
        var rules = await _context.TagRules
            .Include(r => r.Sources).ThenInclude(s => s.SourceTag)
            .Include(r => r.Cases)
            .Include(r => r.TargetTag)
            .Where(r => r.UserId == userId && r.IsEnabled && r.Sources.Any(s => tagIds.Contains(s.SourceTagId)))
            .ToListAsync();

        if (rules.Count == 0)
        {
            return;
        }

        foreach (var rule in rules)
        {
            var sourceIds = rule.Sources.Select(s => s.SourceTagId).ToHashSet();
            var days = changes
                .Where(c => sourceIds.Contains(c.TagId) && c.Day >= rule.EffectiveFrom)
                .Select(c => c.Day)
                .Distinct();

            foreach (var day in days)
            {
                await EvaluateDays(rule, day, day, dryRun: false);
            }
        }

        await _context.SaveChangesAsync();
    }

    public Task<TagRuleRangeResult> Preview(TagRule rule, DateOnly from, DateOnly to)
    {
        return EvaluateDays(rule, from, to, dryRun: true);
    }

    public async Task<TagRuleRangeResult> Recompute(TagRule rule, DateOnly from, DateOnly to)
    {
        var total = TagRuleRangeResult.Empty;
        var monthStart = from;
        while (monthStart <= to)
        {
            var nextMonth = new DateOnly(monthStart.Year, monthStart.Month, 1).AddMonths(1);
            var monthEnd = nextMonth.AddDays(-1) < to ? nextMonth.AddDays(-1) : to;

            await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync()
                : null;

            total = total.Add(await EvaluateDays(rule, monthStart, monthEnd, dryRun: false));
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            monthStart = nextMonth;
        }

        return total;
    }

    // Set-based: loads the range's source rows and existing results once, groups the sources by
    // local day and brings each day's result in line — insert, update or delete. A day without a
    // contributing source row has no result (no zero rows). A dry run only counts. Changes are
    // tracked but not saved; the caller saves.
    private async Task<TagRuleRangeResult> EvaluateDays(TagRule rule, DateOnly from, DateOnly to, bool dryRun)
    {
        var factors = rule.Sources.ToDictionary(s => s.SourceTagId, s => s.Factor);
        var sourceIds = factors.Keys.ToList();
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var sourceRows = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == rule.UserId && sourceIds.Contains(a.TagId) && a.DateStarted >= start && a.DateStarted < end)
            .Select(a => new { a.Id, a.TagId, a.DateStarted, a.Description })
            .ToListAsync();

        var fromKey = WindowKey(from);
        var toKey = WindowKey(to);
        var resultsQuery = _context.Activities
            .Where(a => a.RuleId == rule.Id && string.Compare(a.WindowKey, fromKey) >= 0 && string.Compare(a.WindowKey, toKey) <= 0);
        var results = dryRun
            ? await resultsQuery.AsNoTracking().ToListAsync()
            : await resultsQuery.ToListAsync();
        var resultsByKey = results
            .Where(r => r.WindowKey != null)
            .GroupBy(r => r.WindowKey!)
            .ToDictionary(g => g.Key, g => g.First());

        var skipped = 0;
        var byDay = new Dictionary<string, List<SourceRow>>();
        foreach (var row in sourceRows)
        {
            var dayKey = WindowKey(DateOnly.FromDateTime(row.DateStarted));
            if (!byDay.TryGetValue(dayKey, out var dayRows))
            {
                byDay[dayKey] = dayRows = new List<SourceRow>();
            }

            dayRows.Add(new SourceRow(row.Id, row.TagId, row.DateStarted, row.Description));
        }

        // The rule's stored value for each day that has one. A day missing here has no value.
        var values = new Dictionary<string, string>();
        foreach (var (dayKey, dayRows) in byDay)
        {
            var (value, daySkipped) = DayValue(rule, factors, dayRows);
            skipped += daySkipped;
            if (value != null)
            {
                values[dayKey] = value;
            }
        }

        int created = 0, updated = 0, deleted = 0, unchanged = 0;
        foreach (var key in values.Keys.Union(resultsByKey.Keys))
        {
            var hasResult = resultsByKey.TryGetValue(key, out var existing);
            if (!values.TryGetValue(key, out var value))
            {
                deleted++;
                if (!dryRun)
                {
                    _context.Activities.Remove(existing!);
                }

                continue;
            }

            if (!hasResult)
            {
                created++;
                if (!dryRun)
                {
                    _context.Activities.Add(new Activity
                    {
                        UserId = rule.UserId,
                        TagId = rule.TargetTagId,
                        DateStarted = DateOnly.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue),
                        DateCreated = DateTime.UtcNow,
                        Description = value,
                        RuleId = rule.Id,
                        WindowKey = key
                    });
                }

                continue;
            }

            if (existing!.Description == value && existing.TagId == rule.TargetTagId)
            {
                unchanged++;
                continue;
            }

            updated++;
            if (!dryRun)
            {
                existing.Description = value;
                existing.TagId = rule.TargetTagId;
            }
        }

        return new TagRuleRangeResult(created, updated, deleted, unchanged, skipped);
    }

    private sealed record SourceRow(int Id, int TagId, DateTime DateStarted, string? Description);

    // One day's stored value by template, and how many non-empty source values were not numbers
    // where a number was needed. "Ignore zero values" leaves out skip markers (a zero, or "no" on
    // a yes/no tag) everywhere, so a day with only skip markers has no value.
    private static (string? Value, int Skipped) DayValue(TagRule rule, Dictionary<int, double> factors, List<SourceRow> rows)
    {
        var yesNoTags = rule.Sources
            .Where(s => s.SourceTag is { InputTypeId: InputTypeIds.Boolean, OptionListId: null })
            .Select(s => s.SourceTagId)
            .ToHashSet();
        var counted = rows.Where(r => !(rule.IgnoreZero && IsSkipMarker(r.Description, yesNoTags.Contains(r.TagId)))).ToList();

        if (rule.Template == TagRuleTemplate.Conditional)
        {
            return (ConditionalValue(rule, counted), 0);
        }

        if (rule.Template == TagRuleTemplate.Aggregate && rule.AggregateKind == TagRuleAggregateKind.Count)
        {
            // Count takes entries of any type, so nothing it sees is skipped.
            return (counted.Count > 0 ? ActivityValueCodec.WriteNumber(rule.TargetTag?.InputTypeId, counted.Count) : null, 0);
        }

        var skipped = 0;
        var numbers = new List<(int TagId, double Value)>();
        foreach (var row in counted)
        {
            if (ActivityValueCodec.TryReadNumber(row.Description, out var number))
            {
                numbers.Add((row.TagId, number));
            }
            else if (!string.IsNullOrWhiteSpace(row.Description))
            {
                skipped++;
            }
        }

        if (numbers.Count == 0)
        {
            return (null, skipped);
        }

        double result = rule.Template switch
        {
            TagRuleTemplate.Aggregate => rule.AggregateKind switch
            {
                TagRuleAggregateKind.Minimum => numbers.Min(n => n.Value),
                TagRuleAggregateKind.Maximum => numbers.Max(n => n.Value),
                _ => numbers.Average(n => n.Value)
            },
            _ => numbers.Sum(n => n.Value * factors[n.TagId])
        };

        return (ActivityValueCodec.WriteNumber(rule.TargetTag?.InputTypeId, result), skipped);
    }

    // The first case whose condition holds on the day value gives the result; none → no value.
    // The day value is the day total for a number source, the day average for a rating, score or
    // percentage, and the latest entry otherwise (an option-list tag is compared by its option).
    // A day without a counted entry has no value at all, so "otherwise" applies only to logged days.
    private static string? ConditionalValue(TagRule rule, List<SourceRow> counted)
    {
        if (counted.Count == 0)
        {
            return null;
        }

        var source = rule.Sources.FirstOrDefault()?.SourceTag;
        var kind = source != null
            ? TagRuleInputTypes.ConditionKind(source.InputTypeId, source.OptionListId != null)
            : TagRuleValueKind.Text;

        double? dayNumber = null;
        string? dayText = null;
        if (kind is TagRuleValueKind.Number or TagRuleValueKind.Scale)
        {
            var numbers = counted
                .Select(r => ActivityValueCodec.TryReadNumber(r.Description, out var n) ? (double?)n : null)
                .Where(n => n.HasValue)
                .Select(n => n!.Value)
                .ToList();
            if (numbers.Count > 0)
            {
                dayNumber = kind == TagRuleValueKind.Scale ? numbers.Average() : numbers.Sum();
            }
        }
        else
        {
            // Entries at the same moment resolve by id, so a recompute always picks the same one.
            dayText = counted.OrderBy(r => r.DateStarted).ThenBy(r => r.Id).Last().Description?.Trim();
        }

        foreach (var ruleCase in rule.Cases.OrderBy(c => c.SortOrder))
        {
            if (Holds(ruleCase, kind, dayNumber, dayText))
            {
                return ruleCase.ResultValue;
            }
        }

        return null;
    }

    // Day totals and averages carry binary rounding (0.1 + 0.2 is not exactly 0.3), so equality
    // allows a relative tolerance far below any value a person logs.
    private const double Tolerance = 1e-9;

    private static bool Holds(TagRuleCase ruleCase, TagRuleValueKind kind, double? dayNumber, string? dayText)
    {
        switch (ruleCase.Operator)
        {
            case TagRuleOperator.IsLogged:
            case TagRuleOperator.Otherwise:
                return true;
            case TagRuleOperator.IsYes:
                return ActivityValueCodec.TryReadYesNo(dayText, out var yes) && yes;
            case TagRuleOperator.IsNo:
                return ActivityValueCodec.TryReadYesNo(dayText, out var no) && !no;
        }

        if (kind is TagRuleValueKind.Number or TagRuleValueKind.Scale)
        {
            if (dayNumber is not double day || !ActivityValueCodec.TryReadNumber(ruleCase.Operand, out var operand))
            {
                return false;
            }

            var equal = Math.Abs(day - operand) <= Tolerance * Math.Max(1, Math.Max(Math.Abs(day), Math.Abs(operand)));

            return ruleCase.Operator switch
            {
                TagRuleOperator.Equal => equal,
                TagRuleOperator.NotEqual => !equal,
                TagRuleOperator.Greater => !equal && day > operand,
                TagRuleOperator.GreaterOrEqual => equal || day > operand,
                TagRuleOperator.Less => !equal && day < operand,
                TagRuleOperator.LessOrEqual => equal || day < operand,
                _ => false
            };
        }

        var matches = string.Equals(dayText, ruleCase.Operand?.Trim(), StringComparison.OrdinalIgnoreCase);

        return ruleCase.Operator switch
        {
            TagRuleOperator.Equal => matches,
            TagRuleOperator.NotEqual => !matches,
            _ => false
        };
    }

    private static bool IsSkipMarker(string? description, bool yesNoTag)
    {
        if (yesNoTag)
        {
            return ActivityValueCodec.TryReadYesNo(description, out var yes) && !yes;
        }

        return ActivityValueCodec.TryReadNumber(description, out var number) && number == 0;
    }

    private static string WindowKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
