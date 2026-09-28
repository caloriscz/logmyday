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

        var ruleIds = rules.Select(r => r.Id).ToList();
        var counts = await _context.Activities
            .Where(a => a.RuleId != null && ruleIds.Contains(a.RuleId.Value))
            .GroupBy(a => a.RuleId!.Value)
            .Select(g => new { RuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(c => c.RuleId, c => c.Count);

        return rules.Select(r =>
        {
            var response = MapToResponse(r);
            response.ResultCount = counts.GetValueOrDefault(r.Id);

            return response;
        }).ToList();
    }

    public async Task<TagRuleResponse> GetById(int id, Guid userId)
    {
        return await MapWithCount(await LoadRule(id, userId));
    }

    public async Task<TagRuleResponse> Create(TagRuleRequest request, Guid userId)
    {
        var (target, sources, cases) = await Validate(request, userId, ruleId: null);
        var today = await UserToday(userId);
        var now = DateTime.UtcNow;

        var rule = new TagRule
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Template = request.Template,
            AggregateKind = request.Template == TagRuleTemplate.Aggregate ? request.AggregateKind : null,
            TargetTagId = target.Id,
            IgnoreZero = request.IgnoreZero,
            IsEnabled = request.IsEnabled,
            EffectiveFrom = today,
            DateCreated = now,
            DateUpdated = now,
            Sources = request.Sources
                .Select(s => new TagRuleSource { SourceTagId = s.SourceTagId, Factor = FactorFor(request, s) })
                .ToList(),
            Cases = cases
        };

        target.IsComputed = true;
        _context.TagRules.Add(rule);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Rule '{rule.Name}' created: '{target.TagName}' = {Describe(rule, sources)}, active from {today:yyyy-MM-dd}");

        var saved = await LoadRule(rule.Id, userId);
        if (saved.IsEnabled)
        {
            await _engine.Recompute(saved, today, today);
        }

        return await MapWithCount(saved);
    }

    public async Task<TagRuleResponse> Update(int id, TagRuleRequest request, Guid userId)
    {
        var rule = await LoadRule(id, userId);
        if (request.TargetTagId != rule.TargetTagId)
        {
            throw new ArgumentException("The target tag of a rule cannot change. Delete the rule and create a new one.");
        }

        var (target, sources, cases) = await Validate(request, userId, ruleId: id);

        rule.Name = request.Name.Trim();
        rule.Template = request.Template;
        rule.AggregateKind = request.Template == TagRuleTemplate.Aggregate ? request.AggregateKind : null;
        rule.IgnoreZero = request.IgnoreZero;
        rule.IsEnabled = request.IsEnabled;
        rule.DateUpdated = DateTime.UtcNow;

        // Sources are matched by tag, so an unchanged source keeps its row (unique per rule and tag).
        var requested = request.Sources.ToDictionary(s => s.SourceTagId, s => FactorFor(request, s));
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

        // Cases are rewritten by position, so the unique (rule, sort order) index never sees a clash.
        var existingCases = rule.Cases.OrderBy(c => c.SortOrder).ToList();
        for (var i = 0; i < Math.Max(existingCases.Count, cases.Count); i++)
        {
            if (i >= cases.Count)
            {
                rule.Cases.Remove(existingCases[i]);
                _context.TagRuleCases.Remove(existingCases[i]);
            }
            else if (i >= existingCases.Count)
            {
                cases[i].RuleId = rule.Id;
                rule.Cases.Add(cases[i]);
            }
            else
            {
                existingCases[i].Operator = cases[i].Operator;
                existingCases[i].Operand = cases[i].Operand;
                existingCases[i].ResultValue = cases[i].ResultValue;
            }
        }

        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Rule '{rule.Name}' updated: '{target.TagName}' = {Describe(rule, sources)}{(rule.IsEnabled ? string.Empty : " (paused)")}");

        // A paused rule keeps its results but stops updating them. An enabled rule gets today in
        // line with its new definition; earlier days are recomputed only on request, after the
        // editor has shown a preview of what changes.
        var saved = await LoadRule(id, userId);
        if (saved.IsEnabled)
        {
            var today = await UserToday(userId);
            await _engine.Recompute(saved, today, today);
        }

        return await MapWithCount(saved);
    }

    public async Task<TagRuleRangeResponse> Preview(int id, DateOnly? from, DateOnly? to, Guid userId)
    {
        var rule = await LoadRule(id, userId);
        var firstSourceDate = await FirstSourceDate(rule);
        var today = await UserToday(userId);
        var (start, end) = ValidateRange(from ?? firstSourceDate ?? today, to ?? today, today);

        var result = await _engine.Preview(rule, start, end);

        return await MapRange(rule, start, end, result, firstSourceDate);
    }

    public async Task<TagRuleRangeResponse> Recompute(int id, TagRuleRecomputeRequest request, Guid userId)
    {
        var rule = await LoadRule(id, userId);
        if (!rule.IsEnabled)
        {
            throw new ArgumentException("The rule is paused. Activate it before recomputing.");
        }

        var today = await UserToday(userId);
        var (start, end) = ValidateRange(request.From, request.To, today);
        var started = DateTime.UtcNow;

        var result = await _engine.Recompute(rule, start, end);

        // The recomputed days now belong to the rule's active range and follow source edits.
        if (start < rule.EffectiveFrom)
        {
            rule.EffectiveFrom = start;
            rule.DateUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Rule '{rule.Name}' recomputed {start:yyyy-MM-dd}..{end:yyyy-MM-dd}: {result.Created} created, {result.Updated} updated, {result.Deleted} removed, {result.Unchanged} unchanged, {result.SkippedSourceValues} non-numeric source value(s) skipped ({(DateTime.UtcNow - started).TotalMilliseconds:0} ms)");

        return await MapRange(rule, start, end, result, await FirstSourceDate(rule));
    }

    private static (DateOnly Start, DateOnly End) ValidateRange(DateOnly from, DateOnly to, DateOnly today)
    {
        if (from > to)
        {
            throw new ArgumentException("The start date must not be after the end date.");
        }

        if (to > today)
        {
            throw new ArgumentException("Days after today cannot be recomputed.");
        }

        return (from, to);
    }

    private async Task<DateOnly?> FirstSourceDate(TagRule rule)
    {
        var sourceIds = rule.Sources.Select(s => s.SourceTagId).ToList();
        var first = await _context.Activities
            .Where(a => a.UserId == rule.UserId && sourceIds.Contains(a.TagId))
            .OrderBy(a => a.DateStarted)
            .Select(a => (DateTime?)a.DateStarted)
            .FirstOrDefaultAsync();

        return first.HasValue ? DateOnly.FromDateTime(first.Value) : null;
    }

    private async Task<TagRuleRangeResponse> MapRange(TagRule rule, DateOnly start, DateOnly end, TagRuleRangeResult result, DateOnly? firstSourceDate)
    {
        var startKey = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        return new TagRuleRangeResponse
        {
            From = start,
            To = end,
            Created = result.Created,
            Updated = result.Updated,
            Deleted = result.Deleted,
            Unchanged = result.Unchanged,
            SkippedSourceValues = result.SkippedSourceValues,
            FirstSourceDate = firstSourceDate,
            ResultsBeforeFrom = await _context.Activities.CountAsync(a => a.RuleId == rule.Id && string.Compare(a.WindowKey, startKey) < 0)
        };
    }

    private async Task<TagRuleResponse> MapWithCount(TagRule rule)
    {
        var response = MapToResponse(rule);
        response.ResultCount = await _context.Activities.CountAsync(a => a.RuleId == rule.Id);

        return response;
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

    // A rule reads tags the caller owns and writes a numeric target tag that belongs to this rule
    // alone. Weighted sum: number sources. Aggregate: number, rating, score or percentage sources,
    // or any tag for Count. No chaining: a computed tag cannot be a source.
    private async Task<(Tag Target, Dictionary<int, Tag> Sources, List<TagRuleCase> Cases)> Validate(TagRuleRequest request, Guid userId, int? ruleId)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            throw new ArgumentException("A rule needs a name of at most 100 characters.");
        }

        if (request.Template == TagRuleTemplate.Conditional && request.Sources.Count != 1)
        {
            throw new ArgumentException("A conditional rule reads exactly one source tag.");
        }

        if (request.Template == TagRuleTemplate.Aggregate && request.AggregateKind == null)
        {
            throw new ArgumentException("Choose what the rule calculates: average, minimum, maximum or count.");
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

        if (request.Template == TagRuleTemplate.Conditional)
        {
            if (!IsConditionalTarget(target))
            {
                throw new ArgumentException($"The result tag '{target.TagName}' must be a text, option list, yes/no or number tag.");
            }
        }
        else if (!IsNumeric(target))
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

            if (SourceTypeError(request, source) is string typeError)
            {
                throw new ArgumentException(typeError);
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

        var cases = request.Template == TagRuleTemplate.Conditional
            ? await ValidateCases(request, sources.Values.Single(), target)
            : new List<TagRuleCase>();

        return (target, sources, cases);
    }

    private static bool IsNumeric(Tag tag) => tag.InputTypeId is 1 or 6;

    // Numbers plus the integer-valued ratings, scores and percentage (input types 7–11).
    private static bool IsNumericLike(Tag tag) => tag.InputTypeId is 1 or 6 or 7 or 8 or 9 or 10 or 11;

    private static string? SourceTypeError(TagRuleRequest request, Tag source)
    {
        return request.Template switch
        {
            TagRuleTemplate.Aggregate when request.AggregateKind == TagRuleAggregateKind.Count => null,
            TagRuleTemplate.Aggregate when !IsNumericLike(source) =>
                $"The source tag '{source.TagName}' must be a number, rating, score or percentage tag.",
            TagRuleTemplate.WeightedSum when !IsNumeric(source) =>
                $"The source tag '{source.TagName}' must be a number tag (Integer or Decimal).",
            _ => null
        };
    }

    private const int MaxCases = 20;

    // A conditional result can be text, one option of a list, yes/no or a number.
    private static bool IsConditionalTarget(Tag tag) => tag.OptionListId != null || tag.InputTypeId is 1 or 2 or 3 or 6;

    // Checks each case against the source's type and normalises its result to the target's stored
    // encoding. The first matching case wins at evaluation, so overlap is never ambiguous.
    private async Task<List<TagRuleCase>> ValidateCases(TagRuleRequest request, Tag source, Tag target)
    {
        if (request.Cases.Count == 0)
        {
            throw new ArgumentException("A conditional rule needs at least one case.");
        }

        if (request.Cases.Count > MaxCases)
        {
            throw new ArgumentException($"A rule can have at most {MaxCases} cases.");
        }

        var numericSource = IsNumericLike(source);
        var booleanSource = source.InputTypeId == 3 && source.OptionListId == null;
        var options = target.OptionListId is int listId
            ? await _context.TagOptions.Where(o => o.OptionListId == listId).ToListAsync()
            : null;

        var cases = new List<TagRuleCase>();
        for (var i = 0; i < request.Cases.Count; i++)
        {
            var requested = request.Cases[i];
            var position = $"Case {i + 1}";
            var op = requested.Operator;

            if (op == TagRuleOperator.Otherwise && i != request.Cases.Count - 1)
            {
                throw new ArgumentException($"{position}: \"otherwise\" can only be the last case.");
            }

            var operand = requested.Operand?.Trim();
            switch (op)
            {
                case TagRuleOperator.IsLogged:
                case TagRuleOperator.Otherwise:
                    operand = null;
                    break;
                case TagRuleOperator.IsYes:
                case TagRuleOperator.IsNo:
                    if (!booleanSource)
                    {
                        throw new ArgumentException($"{position}: \"is yes\" and \"is no\" need a yes/no source tag.");
                    }

                    if (op == TagRuleOperator.IsNo && request.IgnoreZero)
                    {
                        throw new ArgumentException($"{position}: \"is no\" cannot match while \"Ignore zero values\" leaves out \"no\" entries. Turn that option off.");
                    }

                    operand = null;
                    break;
                case TagRuleOperator.Equal:
                case TagRuleOperator.NotEqual:
                    if (booleanSource)
                    {
                        throw new ArgumentException($"{position}: use \"is yes\" or \"is no\" for a yes/no source tag.");
                    }

                    if (string.IsNullOrEmpty(operand) || (numericSource && !ActivityValueCodec.TryReadNumber(operand, out _)))
                    {
                        throw new ArgumentException($"{position}: enter {(numericSource ? "a number" : "a value")} to compare with.");
                    }

                    break;
                default:
                    if (!numericSource)
                    {
                        throw new ArgumentException($"{position}: greater and less than need a number, rating, score or percentage source tag.");
                    }

                    if (!ActivityValueCodec.TryReadNumber(operand, out _))
                    {
                        throw new ArgumentException($"{position}: enter a number to compare with.");
                    }

                    break;
            }

            cases.Add(new TagRuleCase
            {
                SortOrder = i,
                Operator = op,
                Operand = operand,
                ResultValue = NormaliseResult(requested.ResultValue, target, options, position)
            });
        }

        return cases;
    }

    private static string NormaliseResult(string? value, Tag target, List<TagOption>? options, string position)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new ArgumentException($"{position}: enter the value the result tag gets.");
        }

        if (options != null)
        {
            var option = options.FirstOrDefault(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase)
                || (o.DisplayName != null && string.Equals(o.DisplayName, text, StringComparison.OrdinalIgnoreCase)));

            return option?.Value
                ?? throw new ArgumentException($"{position}: '{text}' is not an option of '{target.TagName}'.");
        }

        switch (target.InputTypeId)
        {
            case 3:
                return text.ToLowerInvariant() switch
                {
                    "true" or "yes" => "true",
                    "false" or "no" => "false",
                    _ => throw new ArgumentException($"{position}: '{target.TagName}' takes yes or no.")
                };
            case 1 or 6:
                return ActivityValueCodec.TryReadNumber(text, out var number)
                    ? ActivityValueCodec.WriteNumber(target.InputTypeId, number)
                    : throw new ArgumentException($"{position}: '{target.TagName}' takes a number.");
            default:
                return text.Length <= 500
                    ? text
                    : throw new ArgumentException($"{position}: the text is longer than 500 characters.");
        }
    }

    // Factors belong to the weighted sum; other templates store 1.
    private static double FactorFor(TagRuleRequest request, TagRuleSourceRequest source)
    {
        return request.Template == TagRuleTemplate.WeightedSum ? source.Factor : 1;
    }

    private IQueryable<TagRule> RulesWithTags()
    {
        return _context.TagRules
            .Include(r => r.TargetTag).ThenInclude(t => t!.Unit)
            .Include(r => r.TargetTag).ThenInclude(t => t!.Group)
            .Include(r => r.Sources).ThenInclude(s => s.SourceTag).ThenInclude(t => t!.Unit)
            .Include(r => r.Sources).ThenInclude(s => s.SourceTag).ThenInclude(t => t!.Group)
            .Include(r => r.Cases);
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
        string Name(int tagId) => $"'{(sources.TryGetValue(tagId, out var t) ? t.TagName : "?")}'";

        if (rule.Template == TagRuleTemplate.Aggregate)
        {
            var kind = rule.AggregateKind?.ToString().ToLowerInvariant() ?? "aggregate";

            return $"{kind} of {string.Join(", ", rule.Sources.Select(s => Name(s.SourceTagId)))}";
        }

        if (rule.Template == TagRuleTemplate.Conditional)
        {
            var source = rule.Sources.Select(s => Name(s.SourceTagId)).FirstOrDefault() ?? "?";

            return string.Join("; ", rule.Cases.OrderBy(c => c.SortOrder)
                .Select(c => $"'{c.ResultValue}' if {source} {c.Operator}{(c.Operand != null ? " " + c.Operand : string.Empty)}"));
        }

        return string.Join(" + ", rule.Sources.Select(s =>
            $"{s.Factor.ToString(System.Globalization.CultureInfo.InvariantCulture)} × {Name(s.SourceTagId)}"));
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
            AggregateKind = rule.AggregateKind?.ToString(),
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
                .ToList(),
            Cases = rule.Cases
                .OrderBy(c => c.SortOrder)
                .Select(c => new TagRuleCaseResponse
                {
                    Operator = c.Operator.ToString(),
                    Operand = c.Operand,
                    ResultValue = c.ResultValue
                })
                .ToList()
        };
    }
}
