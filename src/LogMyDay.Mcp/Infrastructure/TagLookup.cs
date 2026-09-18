using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// Resolves the tag an agent names the way the CLI does, so "Vitamin D", "Health:Vitamin D",
/// ":Vitamin D" and "42" all mean the same thing in both places. The qualified title a grouped tag
/// carries is "Group: Name" (as <see cref="ITagService.GetAll"/> returns it).
/// </summary>
public sealed class TagLookup(ITagService tags)
{
    public sealed record Result(TagResponse? Match, IReadOnlyList<TagResponse> Candidates)
    {
        public bool IsAmbiguous => Match == null && Candidates.Count > 1;
    }

    /// <summary>The match, or a not-found / ambiguous error the mapper relays with the candidates.</summary>
    public async Task<TagResponse> Require(string query, Guid userId)
    {
        var result = Resolve(await tags.GetAll(userId), query);
        if (result.Match != null)
        {
            return result.Match;
        }

        if (result.IsAmbiguous)
        {
            var names = string.Join(", ", result.Candidates.Select(c => $"{c.Id} '{c.Title}'"));

            throw new ArgumentException($"Tag '{query}' is ambiguous; candidates: {names}. Use the id or the exact title.");
        }

        throw new KeyNotFoundException($"No tag matches '{query}'. Use find_tag or list_tags to see what exists.");
    }

    public async Task<Result> Find(string query, Guid userId) => Resolve(await tags.GetAll(userId), query);

    public static Result Resolve(IEnumerable<TagResponse> all, string query)
    {
        var list = all as IList<TagResponse> ?? all.ToList();
        query = query.Trim();

        if (query.Length == 0)
        {
            return new Result(null, []);
        }

        // Numeric id: exact or nothing.
        if (int.TryParse(query, out var id))
        {
            return Single(list.FirstOrDefault(t => t.Id == id));
        }

        // ":name" — an ungrouped tag by exact name.
        if (query.StartsWith(':'))
        {
            var name = query[1..].Trim();

            return Single(list.FirstOrDefault(t => t.GroupId == null && Equal(t.Title, name)));
        }

        // "group:name" — exact qualified title; no fuzzy fallback on a miss.
        var colon = query.IndexOf(':');
        if (colon > 0)
        {
            var qualified = $"{query[..colon].Trim()}: {query[(colon + 1)..].Trim()}";

            return Single(list.FirstOrDefault(t => Equal(t.Title, qualified)));
        }

        // Plain name: exact → single starts-with → single contains; otherwise report the candidates.
        var exact = list.FirstOrDefault(t => Equal(t.Title, query) || Equal(BareName(t), query));
        if (exact != null)
        {
            return Single(exact);
        }

        var startsWith = list.Where(t => t.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                                         || BareName(t).StartsWith(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (startsWith.Count == 1)
        {
            return Single(startsWith[0]);
        }

        var contains = list.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        return contains.Count == 1 ? Single(contains[0]) : new Result(null, contains);
    }

    private static Result Single(TagResponse? tag) => tag == null ? new Result(null, []) : new Result(tag, [tag]);

    private static bool Equal(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The name without its "Group: " prefix, so a plain query still finds a grouped tag.</summary>
    private static string BareName(TagResponse tag)
    {
        if (tag.GroupName != null && tag.Title.StartsWith(tag.GroupName + ": ", StringComparison.Ordinal))
        {
            return tag.Title[(tag.GroupName.Length + 2)..];
        }

        return tag.Title;
    }
}
