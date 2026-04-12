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

        // Exact case-insensitive match
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
