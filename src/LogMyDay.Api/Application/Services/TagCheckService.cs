using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class TagCheckService : ITagCheckService
{
    public const int DefaultReviewDays = 30;
    public const int MaxReviewDays = 366;

    private readonly LogMyDayDbContext _context;
    private readonly IEventLogService _eventLogService;

    public TagCheckService(LogMyDayDbContext context, IEventLogService eventLogService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
    }

    public async Task<IList<TagCheckResponse>> GetAll(Guid userId)
    {
        var checks = await ChecksWithTags()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return checks.Select(MapToResponse).ToList();
    }

    public async Task<TagCheckResponse> GetById(int id, Guid userId)
    {
        return MapToResponse(await LoadCheck(id, userId));
    }

    public async Task<TagCheckResponse> Create(TagCheckRequest request, Guid userId)
    {
        var (ifTag, thenTag, ifOperand, thenOperand) = await Validate(request, userId);
        var now = DateTime.UtcNow;

        var check = new TagCheck
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Severity = request.Severity,
            IsEnabled = request.IsEnabled,
            IfTagId = ifTag.Id,
            IfOperator = request.IfOperator,
            IfOperand = ifOperand,
            ThenTagId = thenTag.Id,
            ThenOperator = request.ThenOperator,
            ThenOperand = thenOperand,
            DateCreated = now,
            DateUpdated = now
        };

        _context.TagChecks.Add(check);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Check '{check.Name}' created: {Describe(check, ifTag, thenTag)}");

        return MapToResponse(await LoadCheck(check.Id, userId));
    }

    public async Task<TagCheckResponse> Update(int id, TagCheckRequest request, Guid userId)
    {
        var check = await LoadCheck(id, userId);
        var (ifTag, thenTag, ifOperand, thenOperand) = await Validate(request, userId);

        check.Name = request.Name.Trim();
        check.Severity = request.Severity;
        check.IsEnabled = request.IsEnabled;
        check.IfTagId = ifTag.Id;
        check.IfOperator = request.IfOperator;
        check.IfOperand = ifOperand;
        check.ThenTagId = thenTag.Id;
        check.ThenOperator = request.ThenOperator;
        check.ThenOperand = thenOperand;
        check.DateUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Check '{check.Name}' updated: {Describe(check, ifTag, thenTag)}{(check.IsEnabled ? string.Empty : " (paused)")}");

        return MapToResponse(await LoadCheck(id, userId));
    }

    public async Task Delete(int id, Guid userId)
    {
        var check = await LoadCheck(id, userId);
        _context.TagChecks.Remove(check);
        await _context.SaveChangesAsync();

        await _eventLogService.Log(userId, EventLogLevel.Info, $"Check '{check.Name}' deleted");
    }

    public async Task<TagCheckReviewResponse> Review(DateOnly? from, DateOnly? to, Guid userId)
    {
        var today = await UserDay.Today(_context, userId);
        var end = to ?? today;
        var start = from ?? end.AddDays(-(DefaultReviewDays - 1));

        if (start > end)
        {
            throw new ArgumentException("The start date must not be after the end date.");
        }

        if (end > today)
        {
            throw new ArgumentException("Days after today cannot be reviewed.");
        }

        if (end.DayNumber - start.DayNumber + 1 > MaxReviewDays)
        {
            throw new ArgumentException($"Review at most {MaxReviewDays} days at a time.");
        }

        var checks = await ChecksWithTags()
            .Where(c => c.UserId == userId && c.IsEnabled)
            .OrderBy(c => c.Name)
            .ToListAsync();
        var response = new TagCheckReviewResponse { From = start, To = end, CheckCount = checks.Count };
        if (checks.Count == 0)
        {
            return response;
        }

        // Set-based: the range's entries of every tag the checks read, loaded once and grouped by
        // tag and local day.
        var tagIds = checks.SelectMany(c => new[] { c.IfTagId, c.ThenTagId }).Distinct().ToList();
        var startAt = start.ToDateTime(TimeOnly.MinValue);
        var endAt = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == userId && tagIds.Contains(a.TagId) && a.DateStarted >= startAt && a.DateStarted < endAt)
            .Select(a => new { a.Id, a.TagId, a.DateStarted, a.Description })
            .ToListAsync();
        var entries = rows
            .GroupBy(r => (r.TagId, Day: DateOnly.FromDateTime(r.DateStarted)))
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<TagDayValue.Entry>)g
                .Select(r => new TagDayValue.Entry(r.Id, r.DateStarted, r.Description))
                .ToList());
        IReadOnlyCollection<TagDayValue.Entry> EntriesOf(int tagId, DateOnly day) =>
            entries.TryGetValue((tagId, day), out var found) ? found : [];

        foreach (var check in checks)
        {
            var days = entries.Keys.Where(k => k.TagId == check.IfTagId).Select(k => k.Day);
            foreach (var day in days)
            {
                var result = TagCheckEvaluator.Evaluate(check, check.IfTag!, check.ThenTag!,
                    EntriesOf(check.IfTagId, day), EntriesOf(check.ThenTagId, day), day == today);
                if (result.Outcome is not (TagCheckOutcome.Broken or TagCheckOutcome.Pending))
                {
                    continue;
                }

                response.Items.Add(new TagCheckReviewItem
                {
                    Date = day,
                    CheckId = check.Id,
                    CheckName = check.Name,
                    Severity = check.Severity.ToString(),
                    IsPending = result.Outcome == TagCheckOutcome.Pending,
                    IfTagName = TagTitle(check.IfTag),
                    IfValue = result.IfValue,
                    ThenTagName = TagTitle(check.ThenTag),
                    ThenValue = result.ThenValue
                });
            }
        }

        response.Items = response.Items
            .OrderByDescending(i => i.Date)
            .ThenBy(i => i.CheckName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return response;
    }

    // Both tags belong to the caller; the rest is checked by TagCheckValidator, shared with the
    // backup import.
    private async Task<(Tag IfTag, Tag ThenTag, string? IfOperand, string? ThenOperand)> Validate(TagCheckRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > TagCheckValidator.MaxNameLength)
        {
            throw new ArgumentException($"A check needs a name of at most {TagCheckValidator.MaxNameLength} characters.");
        }

        var tags = await _context.Tags
            .Where(t => (t.Id == request.IfTagId || t.Id == request.ThenTagId) && t.UserId == userId)
            .ToDictionaryAsync(t => t.Id);
        if (!tags.TryGetValue(request.IfTagId, out var ifTag) || !tags.TryGetValue(request.ThenTagId, out var thenTag))
        {
            throw new KeyNotFoundException("Tag not found");
        }

        var (ifOperand, thenOperand) = TagCheckValidator.Validate(request.Severity,
            request.IfOperator, request.IfOperand, request.ThenOperator, request.ThenOperand,
            ifTag, thenTag, await OptionsOf(ifTag), await OptionsOf(thenTag));

        return (ifTag, thenTag, ifOperand, thenOperand);
    }

    private async Task<List<TagOption>?> OptionsOf(Tag tag)
    {
        return tag.OptionListId is int listId
            ? await _context.TagOptions.Where(o => o.OptionListId == listId).ToListAsync()
            : null;
    }

    private IQueryable<TagCheck> ChecksWithTags()
    {
        return _context.TagChecks
            .Include(c => c.IfTag).ThenInclude(t => t!.Group)
            .Include(c => c.ThenTag).ThenInclude(t => t!.Group);
    }

    private async Task<TagCheck> LoadCheck(int id, Guid userId)
    {
        return await ChecksWithTags().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId)
            ?? throw new KeyNotFoundException("Check not found");
    }

    /// <summary>The kind the editor shows: the two shortcuts, or a general if-then check.</summary>
    public static string KindOf(TagCheck check) => (check.IfOperator, check.ThenOperator) switch
    {
        (TagRuleOperator.IsLogged, TagRuleOperator.IsNotLogged) => "NotOnTheSameDay",
        (TagRuleOperator.IsLogged, TagRuleOperator.IsLogged) => "Requires",
        _ => "IfThen"
    };

    private static string Describe(TagCheck check, Tag ifTag, Tag thenTag)
    {
        return $"if {Side(ifTag, check.IfOperator, check.IfOperand)} then {Side(thenTag, check.ThenOperator, check.ThenOperand)} ({check.Severity.ToString().ToLowerInvariant()})";
    }

    private static string Side(Tag tag, TagRuleOperator op, string? operand) => op switch
    {
        TagRuleOperator.IsLogged => $"'{tag.TagName}' is logged",
        TagRuleOperator.IsNotLogged => $"'{tag.TagName}' is not logged",
        TagRuleOperator.IsYes => $"'{tag.TagName}' is yes",
        TagRuleOperator.IsNo => $"'{tag.TagName}' is no",
        _ => $"'{tag.TagName}' {Symbol(op)} {operand}"
    };

    private static string Symbol(TagRuleOperator op) => op switch
    {
        TagRuleOperator.Equal => "=",
        TagRuleOperator.NotEqual => "≠",
        TagRuleOperator.Greater => ">",
        TagRuleOperator.GreaterOrEqual => "≥",
        TagRuleOperator.Less => "<",
        TagRuleOperator.LessOrEqual => "≤",
        _ => op.ToString()
    };

    private static string TagTitle(Tag? tag)
    {
        if (tag == null)
        {
            return string.Empty;
        }

        return tag.Group?.Name != null ? $"{tag.Group.Name}: {tag.TagName}" : tag.TagName;
    }

    private static TagCheckResponse MapToResponse(TagCheck check)
    {
        return new TagCheckResponse
        {
            Id = check.Id,
            Name = check.Name,
            Kind = KindOf(check),
            Severity = check.Severity.ToString(),
            IsEnabled = check.IsEnabled,
            IfTagId = check.IfTagId,
            IfTagName = TagTitle(check.IfTag),
            IfOperator = check.IfOperator.ToString(),
            IfOperand = check.IfOperand,
            ThenTagId = check.ThenTagId,
            ThenTagName = TagTitle(check.ThenTag),
            ThenOperator = check.ThenOperator.ToString(),
            ThenOperand = check.ThenOperand,
            DateCreated = check.DateCreated,
            DateUpdated = check.DateUpdated
        };
    }
}
