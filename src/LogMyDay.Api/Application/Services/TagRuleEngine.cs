using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
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
            .Include(r => r.Sources)
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
            .Select(a => new { a.TagId, a.DateStarted, a.Description })
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
        var sums = new Dictionary<string, double>();
        foreach (var row in sourceRows)
        {
            if (!ActivityValueCodec.TryReadNumber(row.Description, out var value))
            {
                if (!string.IsNullOrWhiteSpace(row.Description))
                {
                    skipped++;
                }

                continue;
            }

            if (rule.IgnoreZero && value == 0)
            {
                continue;
            }

            var key = WindowKey(DateOnly.FromDateTime(row.DateStarted));
            sums[key] = sums.GetValueOrDefault(key) + value * factors[row.TagId];
        }

        int created = 0, updated = 0, deleted = 0, unchanged = 0;
        foreach (var key in sums.Keys.Union(resultsByKey.Keys))
        {
            var hasResult = resultsByKey.TryGetValue(key, out var existing);
            if (!sums.TryGetValue(key, out var sum))
            {
                deleted++;
                if (!dryRun)
                {
                    _context.Activities.Remove(existing!);
                }

                continue;
            }

            var value = ActivityValueCodec.WriteNumber(rule.TargetTag?.InputTypeId, sum);
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

    private static string WindowKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
