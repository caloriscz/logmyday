using System.Globalization;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

/// <summary>
/// Pure computation over one tag's activities. Rows are bucketed by their stored DateStarted date
/// with no time-zone conversion, the same convention the web Insights and the activity list use,
/// so an agent's numbers agree with what the user sees on screen. The user's time zone plays no
/// part here; culture only decides where a week starts.
/// </summary>
public class ActivitySummaryService(LogMyDayDbContext context) : IActivitySummaryService
{
    public const int MaxDaysForDayBucket = 400;
    public const int MaxDaysForWeekBucket = 1100;
    public const int MaxDaysForMonthBucket = 3700;
    public const int MaxFrequencies = 20;

    public const string Numeric = "numeric";
    public const string Boolean = "boolean";
    public const string Categorical = "categorical";
    public const string Text = "text";
    public const string DateKind = "date";
    public const string TimeKind = "time";

    public async Task<ActivitySummaryResponse> Summarize(ActivitySummaryRequest request, Guid userId)
    {
        if (request.To < request.From)
        {
            throw new ArgumentException("to must not be before from.");
        }

        var daysInRange = request.To.DayNumber - request.From.DayNumber + 1;
        EnforceRangeLimit(request.Bucket, daysInRange);

        var tag = await context.Tags.AsNoTracking()
            .Include(t => t.InputType)
            .Include(t => t.Unit)
            .Include(t => t.Group)
            .FirstOrDefaultAsync(t => t.Id == request.TagId && t.UserId == userId)
            ?? throw new KeyNotFoundException("Tag not found");

        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        var weekStartsOn = FirstDayOfWeek(user?.Culture);

        var rangeStart = request.From.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = request.To.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var rows = await context.Activities.AsNoTracking()
            .Where(a => a.UserId == userId && a.TagId == tag.Id && a.DateStarted >= rangeStart && a.DateStarted < rangeEnd)
            .OrderBy(a => a.DateStarted)
            .ThenBy(a => a.Id)
            .ToListAsync();

        var kind = ValueKindOf(tag);
        var parsed = rows.Select(r => new Row(r, DateOnly.FromDateTime(r.DateStarted), Parse(kind, r.Description), kind)).ToList();

        var response = new ActivitySummaryResponse
        {
            TagId = tag.Id,
            Tag = tag.Group?.Name != null ? $"{tag.Group.Name}: {tag.TagName}" : tag.TagName,
            InputTypeId = tag.InputTypeId,
            InputType = tag.InputType?.Name,
            ValueKind = kind,
            UnitSymbol = tag.Unit?.Symbol,
            From = request.From,
            To = request.To,
            Bucket = request.Bucket,
            WeekStartsOn = weekStartsOn,
            Count = parsed.Count,
            DaysInRange = daysInRange,
            InvalidValueCount = parsed.Count(p => p.Invalid),
            First = parsed.Count == 0 ? null : Point(parsed[0]),
            Last = parsed.Count == 0 ? null : Point(parsed[^1])
        };

        var numbers = parsed.Where(p => p.Number.HasValue).Select(p => p.Number!.Value).ToList();
        if ((kind is Numeric or Boolean) && numbers.Count > 0)
        {
            response.Sum = numbers.Sum();
            response.Average = numbers.Average();
            if (kind == Numeric)
            {
                response.Min = numbers.Min();
                response.Max = numbers.Max();
            }
        }

        var daysWithData = parsed.Where(HasData).Select(p => p.Date).ToHashSet();
        response.DaysWithData = daysWithData.Count;
        ComputeStreaks(response, daysWithData, request.From, request.To);

        response.Buckets = BuildBuckets(request, parsed, kind, weekStartsOn);

        if (kind is Categorical or Text)
        {
            var valid = parsed.Where(p => !p.Invalid).ToList();
            response.Frequencies = valid
                .GroupBy(p => p.Activity.Description!.Trim())
                .Select(g => new ValueFrequency { Value = g.Key, Count = g.Count(), Share = (double)g.Count() / valid.Count })
                .OrderByDescending(f => f.Count).ThenBy(f => f.Value, StringComparer.OrdinalIgnoreCase)
                .Take(MaxFrequencies)
                .ToList();
        }

        return response;
    }

    // --- rules ---

    private static void EnforceRangeLimit(SummaryBucket bucket, int days)
    {
        var (limit, hint) = bucket switch
        {
            SummaryBucket.Day => (MaxDaysForDayBucket, "use bucket Week or Month for longer ranges"),
            SummaryBucket.Week => (MaxDaysForWeekBucket, "use bucket Month for longer ranges"),
            _ => (MaxDaysForMonthBucket, "split the range")
        };

        if (days > limit)
        {
            throw new ArgumentException($"The range spans {days} days; bucket {bucket} allows at most {limit} — {hint}.");
        }
    }

    public static string ValueKindOf(Tag tag)
    {
        return tag.InputTypeId switch
        {
            InputTypeIds.Integer or InputTypeIds.Decimal or InputTypeIds.StarRating or InputTypeIds.StarRating10
                or InputTypeIds.Percentage or InputTypeIds.Score or InputTypeIds.Score10 => Numeric,
            InputTypeIds.Boolean => Boolean,
            InputTypeIds.Date => DateKind,
            InputTypeIds.Time => TimeKind,
            _ => tag.OptionListId != null ? Categorical : Text
        };
    }

    /// <summary>A parsed value: a number for numeric/boolean kinds, or a validity flag for the rest.</summary>
    private static (double? Number, bool Invalid) Parse(string kind, string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;

        switch (kind)
        {
            case Numeric:
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                    || (text.Count(c => c == ',') == 1 && !text.Contains('.')
                        && double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out n)))
                {
                    return (n, false);
                }

                return (null, true);

            case Boolean:
                return text.ToLowerInvariant() switch
                {
                    "true" or "1" or "yes" => (1, false),
                    "false" or "0" or "no" => (0, false),
                    _ => (null, true)
                };

            case DateKind:
                return (null, !DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _));

            case TimeKind:
                return (null, !TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _));

            default:
                return (null, text.Length == 0);
        }
    }

    /// <summary>A day counts as "with data" when it has a row; for booleans, a true row.</summary>
    private static bool HasData(Row row) => row.Kind == Boolean ? row.Number == 1 : true;

    private static void ComputeStreaks(ActivitySummaryResponse response, HashSet<DateOnly> days, DateOnly from, DateOnly to)
    {
        // Current streak: counted back from To, or from To − 1 when To itself has nothing yet, so a
        // morning query does not zero a streak the user is still keeping.
        var end = days.Contains(to) ? to : to.AddDays(-1);
        var current = 0;
        for (var d = end; d >= from && days.Contains(d); d = d.AddDays(-1))
        {
            current++;
        }

        response.CurrentStreak = current;
        response.CurrentStreakEnd = current > 0 ? end : null;

        var longest = 0;
        DateOnly? longestStart = null;
        var runStart = from;
        var run = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (days.Contains(d))
            {
                if (run == 0)
                {
                    runStart = d;
                }

                run++;
                if (run > longest)
                {
                    longest = run;
                    longestStart = runStart;
                }
            }
            else
            {
                run = 0;
            }
        }

        response.LongestStreak = longest;
        response.LongestStreakStart = longestStart;
        response.LongestStreakEnd = longestStart?.AddDays(longest - 1);
    }

    private static List<SummaryBucketRow> BuildBuckets(ActivitySummaryRequest request, List<Row> rows, string kind, DayOfWeek weekStartsOn)
    {
        var buckets = new List<SummaryBucketRow>();
        var start = request.From;
        while (start <= request.To)
        {
            var next = NextBucketStart(request.Bucket, start, request.From, weekStartsOn);
            var end = next.AddDays(-1) < request.To ? next.AddDays(-1) : request.To;
            var inBucket = rows.Where(r => r.Date >= start && r.Date <= end).ToList();
            var numbers = inBucket.Where(r => r.Number.HasValue).Select(r => r.Number!.Value).ToList();

            var row = new SummaryBucketRow
            {
                Start = start,
                End = end,
                Count = inBucket.Count,
                DaysWithData = inBucket.Where(HasData).Select(r => r.Date).Distinct().Count()
            };
            if ((kind is Numeric or Boolean) && numbers.Count > 0)
            {
                row.Sum = numbers.Sum();
                row.Average = numbers.Average();
                if (kind == Numeric)
                {
                    row.Min = numbers.Min();
                    row.Max = numbers.Max();
                }
            }

            buckets.Add(row);
            start = next;
        }

        return buckets;
    }

    /// <summary>
    /// Buckets are aligned to calendar weeks/months, so the first and last may be partial; the first
    /// bucket starts at From regardless.
    /// </summary>
    private static DateOnly NextBucketStart(SummaryBucket bucket, DateOnly current, DateOnly from, DayOfWeek weekStartsOn)
    {
        switch (bucket)
        {
            case SummaryBucket.Week:
                var offset = ((int)current.DayOfWeek - (int)weekStartsOn + 7) % 7;

                return current.AddDays(7 - offset);

            case SummaryBucket.Month:
                return new DateOnly(current.Year, current.Month, 1).AddMonths(1);

            default:
                return current.AddDays(1);
        }
    }

    public static DayOfWeek FirstDayOfWeek(string? culture)
    {
        try
        {
            return new CultureInfo(culture ?? "en-US").DateTimeFormat.FirstDayOfWeek;
        }
        catch (CultureNotFoundException)
        {
            return DayOfWeek.Monday;
        }
    }

    private static SummaryPoint Point(Row row) => new() { Value = row.Activity.Description ?? string.Empty, DateStarted = row.Activity.DateStarted };

    private sealed record Row(Activity Activity, DateOnly Date, (double? Number, bool Invalid) Parsed, string Kind)
    {
        public double? Number => Parsed.Number;
        public bool Invalid => Parsed.Invalid;
    }
}
