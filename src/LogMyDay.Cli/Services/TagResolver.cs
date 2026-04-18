using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;

namespace LogMyDay.Cli.Services;

public class TagResolver
{
    private List<TagResponse>? _cache;

    public async Task<TagResponse?> ResolveAsync(IActivityApi api, string nameOrId)
    {
        _cache ??= (await api.GetTags()).ToList();

        // Try numeric ID first
        if (int.TryParse(nameOrId, out var id))
        {
            return _cache.FirstOrDefault(t => t.Id == id);
        }

        // Explicit ungrouped syntax: ":tagname"
        // Returns only tags with no group whose raw name matches exactly.
        if (nameOrId.StartsWith(':'))
        {
            var tagName = nameOrId[1..];
            return _cache.FirstOrDefault(t =>
                t.GroupId is null &&
                string.Equals(t.Title, tagName, StringComparison.OrdinalIgnoreCase));
        }

        // Explicit grouped syntax: "group:tagname"
        // Returns only the matching grouped tag. Does NOT fall through to fuzzy on miss.
        var colonIndex = nameOrId.IndexOf(':');
        if (colonIndex > 0)
        {
            var groupPart = nameOrId[..colonIndex].Trim();
            var tagPart = nameOrId[(colonIndex + 1)..].Trim();
            var qualifiedTitle = $"{groupPart}: {tagPart}";

            return _cache.FirstOrDefault(t =>
                string.Equals(t.Title, qualifiedTitle, StringComparison.OrdinalIgnoreCase));
        }

        // Plain name: exact → starts-with → contains fuzzy chain
        var exact = _cache.FirstOrDefault(t =>
            string.Equals(t.Title, nameOrId, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        // Fuzzy: starts-with
        var startsWith = _cache
            .Where(t => t.Title.StartsWith(nameOrId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (startsWith.Count == 1)
        {
            return startsWith[0];
        }

        // Fuzzy: contains
        var contains = _cache
            .Where(t => t.Title.Contains(nameOrId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return contains.Count == 1 ? contains[0] : null;
    }

    public List<TagResponse> GetCachedTags() => _cache ?? [];
}
