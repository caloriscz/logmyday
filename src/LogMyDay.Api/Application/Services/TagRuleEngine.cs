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
                await EvaluateWindow(rule, day);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<(int Created, int Updated, int Deleted)> EvaluateRange(TagRule rule, DateOnly from, DateOnly to)
    {
        var sourceIds = rule.Sources.Select(s => s.SourceTagId).ToList();
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var sourceDays = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == rule.UserId && sourceIds.Contains(a.TagId) && a.DateStarted >= start && a.DateStarted < end)
            .Select(a => a.DateStarted)
            .ToListAsync();

        var fromKey = WindowKey(from);
        var toKey = WindowKey(to);
        var resultKeys = await _context.Activities
            .AsNoTracking()
            .Where(a => a.RuleId == rule.Id && string.Compare(a.WindowKey, fromKey) >= 0 && string.Compare(a.WindowKey, toKey) <= 0)
            .Select(a => a.WindowKey!)
            .ToListAsync();

        var days = sourceDays.Select(DateOnly.FromDateTime)
            .Concat(resultKeys.Select(k => DateOnly.ParseExact(k, "yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .Distinct()
            .OrderBy(d => d);

        var created = 0;
        var updated = 0;
        var deleted = 0;
        foreach (var day in days)
        {
            switch (await EvaluateWindow(rule, day))
            {
                case WindowChange.Created:
                    created++;
                    break;
                case WindowChange.Updated:
                    updated++;
                    break;
                case WindowChange.Deleted:
                    deleted++;
                    break;
            }
        }

        await _context.SaveChangesAsync();

        return (created, updated, deleted);
    }

    // Sums the rule's sources on one local day and brings the result row in line: insert, update
    // or delete. A day without any contributing source row has no result (no zero rows). Changes
    // are tracked but not saved; the caller saves once.
    private async Task<WindowChange> EvaluateWindow(TagRule rule, DateOnly day)
    {
        var factors = rule.Sources.ToDictionary(s => s.SourceTagId, s => s.Factor);
        var sourceIds = factors.Keys.ToList();
        var start = day.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);

        var sourceRows = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == rule.UserId && sourceIds.Contains(a.TagId) && a.DateStarted >= start && a.DateStarted < end)
            .Select(a => new { a.TagId, a.Description })
            .ToListAsync();

        var contributing = 0;
        var sum = 0.0;
        foreach (var row in sourceRows)
        {
            if (!ActivityValueCodec.TryReadNumber(row.Description, out var value) || (rule.IgnoreZero && value == 0))
            {
                continue;
            }

            contributing++;
            sum += value * factors[row.TagId];
        }

        var key = WindowKey(day);
        var existing = _context.Activities.Local.FirstOrDefault(a => a.RuleId == rule.Id && a.WindowKey == key)
            ?? await _context.Activities.FirstOrDefaultAsync(a => a.RuleId == rule.Id && a.WindowKey == key);

        if (contributing == 0)
        {
            if (existing == null)
            {
                return WindowChange.None;
            }

            _context.Activities.Remove(existing);

            return WindowChange.Deleted;
        }

        var result = ActivityValueCodec.WriteNumber(rule.TargetTag?.InputTypeId, sum);
        if (existing == null)
        {
            _context.Activities.Add(new Activity
            {
                UserId = rule.UserId,
                TagId = rule.TargetTagId,
                DateStarted = start,
                DateCreated = DateTime.UtcNow,
                Description = result,
                RuleId = rule.Id,
                WindowKey = key
            });

            return WindowChange.Created;
        }

        if (existing.Description == result && existing.TagId == rule.TargetTagId)
        {
            return WindowChange.None;
        }

        existing.Description = result;
        existing.TagId = rule.TargetTagId;

        return WindowChange.Updated;
    }

    private static string WindowKey(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private enum WindowChange
    {
        None,
        Created,
        Updated,
        Deleted
    }
}
