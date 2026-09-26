using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class TagRuleService : ITagRuleService
{
    private const int MaxSources = 20;

    private readonly LogMyDayDbContext _context;
    private readonly ITagRuleEngine _engine;
    private readonly IEventLogService _eventLogService;

    public TagRuleService(LogMyDayDbContext context, ITagRuleEngine engine, IEventLogService eventLogService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
    }

    public async Task<IList<TagRuleResponse>> GetAll(Guid userId)
    {
        var rules = await RulesWithTags()
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .ToListAsync();

        return rules.Select(MapToResponse).ToList();
    }

    public async Task<TagRuleResponse> GetById(int id, Guid userId)
    {
        return MapToResponse(await LoadRule(id, userId));
    }

    public async Task<TagRuleResponse> Create(TagRuleRequest request, Guid userId)
    {
        var (target, sources) = await Validate(request, userId, ruleId: null);
        var today = await UserToday(userId);
        var now = DateTime.UtcNow;

        var rule = new TagRule
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Template = TagRuleTemplate.WeightedSum,
            TargetTagId = target.Id,
            IgnoreZero = request.IgnoreZero,
            IsEnabled = request.IsEnabled,
            EffectiveFrom = today,
            DateCreated = now,
            DateUpdated = now,
            Sources = request.Sources
                .Select(s => new TagRuleSource { SourceTagId = s.SourceTagId, Factor = s.Factor })
                .ToList()
        };

        target.IsComputed = true;
        _context.TagRules.Add(rule);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Rule '{rule.Name}' created: '{target.TagName}' = {Describe(rule, sources)}, active from {today:yyyy-MM-dd}");

        var saved = await LoadRule(rule.Id, userId);
        if (saved.IsEnabled)
        {
            await _engine.EvaluateRange(saved, saved.EffectiveFrom, today);
        }

        return MapToResponse(saved);
    }

    public async Task<TagRuleResponse> Update(int id, TagRuleRequest request, Guid userId)
    {
        var rule = await LoadRule(id, userId);
        if (request.TargetTagId != rule.TargetTagId)
        {
            throw new ArgumentException("The target tag of a rule cannot change. Delete the rule and create a new one.");
        }

        var (target, sources) = await Validate(request, userId, ruleId: id);

        rule.Name = request.Name.Trim();
        rule.IgnoreZero = request.IgnoreZero;
        rule.IsEnabled = request.IsEnabled;
        rule.DateUpdated = DateTime.UtcNow;

        // Sources are matched by tag, so an unchanged source keeps its row (unique per rule and tag).
        var requested = request.Sources.ToDictionary(s => s.SourceTagId, s => s.Factor);
        foreach (var existing in rule.Sources.ToList())
        {
            if (requested.Remove(existing.SourceTagId, out var factor))
            {
                existing.Factor = factor;
            }
            else
            {
                rule.Sources.Remove(existing);
                _context.TagRuleSources.Remove(existing);
            }
        }

        foreach (var (sourceTagId, factor) in requested)
        {
            rule.Sources.Add(new TagRuleSource { RuleId = rule.Id, SourceTagId = sourceTagId, Factor = factor });
        }

        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Rule '{rule.Name}' updated: '{target.TagName}' = {Describe(rule, sources)}{(rule.IsEnabled ? string.Empty : " (paused)")}");

        // A paused rule keeps its results but stops updating them. An enabled rule is brought in
        // line with its new definition over its active range.
        var saved = await LoadRule(id, userId);
        if (saved.IsEnabled)
        {
            await _engine.EvaluateRange(saved, saved.EffectiveFrom, await UserToday(userId));
        }

        return MapToResponse(saved);
    }

    public async Task Delete(int id, Guid userId)
    {
        var rule = await LoadRule(id, userId);

        var results = await _context.Activities.Where(a => a.RuleId == rule.Id).ToListAsync();
        _context.Activities.RemoveRange(results);

        if (rule.TargetTag != null)
        {
            rule.TargetTag.IsComputed = false;
        }

        _context.TagRules.Remove(rule);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Rule '{rule.Name}' deleted with {results.Count} generated value(s); '{rule.TargetTag?.TagName ?? "?"}' is a normal tag again");
    }

    // Phase 1 rules: a weighted sum of numeric tags the caller owns, written to a numeric target
    // tag that belongs to this rule alone. No chaining: a computed tag cannot be a source.
    private async Task<(Tag Target, Dictionary<int, Tag> Sources)> Validate(TagRuleRequest request, Guid userId, int? ruleId)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            throw new ArgumentException("A rule needs a name of at most 100 characters.");
        }

        if (request.Sources.Count == 0)
        {
            throw new ArgumentException("A rule needs at least one source tag.");
        }

        if (request.Sources.Count > MaxSources)
        {
            throw new ArgumentException($"A rule can have at most {MaxSources} source tags.");
        }

        if (request.Sources.Select(s => s.SourceTagId).Distinct().Count() != request.Sources.Count)
        {
            throw new ArgumentException("Each source tag can appear only once.");
        }

        if (request.Sources.Any(s => !double.IsFinite(s.Factor)))
        {
            throw new ArgumentException("Every factor must be a number.");
        }

        if (request.Sources.Any(s => s.SourceTagId == request.TargetTagId))
        {
            throw new ArgumentException("The target tag cannot also be a source.");
        }

        var tagIds = request.Sources.Select(s => s.SourceTagId).Append(request.TargetTagId).ToList();
        var tags = await _context.Tags
            .Where(t => tagIds.Contains(t.Id) && t.UserId == userId)
            .ToDictionaryAsync(t => t.Id);

        if (!tags.TryGetValue(request.TargetTagId, out var target))
        {
            throw new KeyNotFoundException("Target tag not found");
        }

        if (!IsNumeric(target))
        {
            throw new ArgumentException($"The target tag '{target.TagName}' must be a number tag (Integer or Decimal).");
        }

        var sources = new Dictionary<int, Tag>();
        foreach (var sourceId in request.Sources.Select(s => s.SourceTagId))
        {
            if (!tags.TryGetValue(sourceId, out var source))
            {
                throw new KeyNotFoundException("Source tag not found");
            }

            if (!IsNumeric(source))
            {
                throw new ArgumentException($"The source tag '{source.TagName}' must be a number tag (Integer or Decimal).");
            }

            if (source.IsComputed)
            {
                throw new ArgumentException($"'{source.TagName}' is computed by another rule and cannot be a source (rules cannot feed rules).");
            }

            sources[sourceId] = source;
        }

        if (ruleId == null)
        {
            if (target.IsComputed || await _context.TagRules.AnyAsync(r => r.TargetTagId == target.Id))
            {
                throw new ArgumentException($"'{target.TagName}' already belongs to another rule.");
            }

            if (await _context.Activities.AnyAsync(a => a.TagId == target.Id))
            {
                throw new ArgumentException($"'{target.TagName}' already has logged values. A rule needs its own empty tag; create a new tag for the result.");
            }

            if (await _context.Reminders.AnyAsync(r => r.CompletionTagId == target.Id)
                || await _context.TodoLists.AnyAsync(l => l.CompletionTagId == target.Id))
            {
                throw new ArgumentException($"'{target.TagName}' is logged by a reminder or todo list. A rule needs its own tag.");
            }
        }

        return (target, sources);
    }

    private static bool IsNumeric(Tag tag) => tag.InputTypeId is 1 or 6;

    private IQueryable<TagRule> RulesWithTags()
    {
        return _context.TagRules
            .Include(r => r.TargetTag).ThenInclude(t => t!.Unit)
            .Include(r => r.TargetTag).ThenInclude(t => t!.Group)
            .Include(r => r.Sources).ThenInclude(s => s.SourceTag).ThenInclude(t => t!.Unit)
            .Include(r => r.Sources).ThenInclude(s => s.SourceTag).ThenInclude(t => t!.Group);
    }

    private async Task<TagRule> LoadRule(int id, Guid userId)
    {
        return await RulesWithTags().FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId)
            ?? throw new KeyNotFoundException("Rule not found");
    }

    private async Task<DateOnly> UserToday(Guid userId)
    {
        var timeZone = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZone)
            .FirstOrDefaultAsync();

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    private static string Describe(TagRule rule, Dictionary<int, Tag> sources)
    {
        return string.Join(" + ", rule.Sources.Select(s =>
            $"{s.Factor.ToString(System.Globalization.CultureInfo.InvariantCulture)} × '{(sources.TryGetValue(s.SourceTagId, out var t) ? t.TagName : "?")}'"));
    }

    private static string TagTitle(Tag? tag)
    {
        if (tag == null)
        {
            return string.Empty;
        }

        return tag.Group?.Name != null ? $"{tag.Group.Name}: {tag.TagName}" : tag.TagName;
    }

    private static TagRuleResponse MapToResponse(TagRule rule)
    {
        return new TagRuleResponse
        {
            Id = rule.Id,
            Name = rule.Name,
            Template = rule.Template.ToString(),
            TargetTagId = rule.TargetTagId,
            TargetTagName = TagTitle(rule.TargetTag),
            TargetUnitSymbol = rule.TargetTag?.Unit?.Symbol,
            IgnoreZero = rule.IgnoreZero,
            IsEnabled = rule.IsEnabled,
            EffectiveFrom = rule.EffectiveFrom,
            DateCreated = rule.DateCreated,
            DateUpdated = rule.DateUpdated,
            Sources = rule.Sources
                .OrderBy(s => s.Id)
                .Select(s => new TagRuleSourceResponse
                {
                    SourceTagId = s.SourceTagId,
                    SourceTagName = TagTitle(s.SourceTag),
                    SourceUnitSymbol = s.SourceTag?.Unit?.Symbol,
                    Factor = s.Factor
                })
                .ToList()
        };
    }
}
