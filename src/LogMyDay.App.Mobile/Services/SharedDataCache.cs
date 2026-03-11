using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile.Services;

/// <summary>
/// Singleton in-memory cache for frequently-fetched, rarely-changing data
/// (tags and option lists). Eliminates redundant API calls across page navigations.
/// </summary>
public interface ISharedDataCache
{
    /// <summary>Returns cached tags or fetches from API if cache is empty / expired.</summary>
    Task<IList<TagResponse>> GetTagsAsync(CancellationToken ct = default);

    /// <summary>Returns cached option lists or fetches from API if cache is empty / expired.</summary>
    Task<IList<TagOptionListResponse>> GetOptionListsAsync(CancellationToken ct = default);

    /// <summary>Seeds the tag cache externally (e.g. from session-restore probe).</summary>
    void SeedTags(IList<TagResponse> tags);

    /// <summary>Invalidates both caches so the next access re-fetches from the API.</summary>
    void Invalidate();
}

public sealed class SharedDataCache : ISharedDataCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IApiClientProvider _apiClientProvider;
    private readonly ILogger<SharedDataCache> _logger;
    private readonly SemaphoreSlim _tagGate = new(1, 1);
    private readonly SemaphoreSlim _optionListGate = new(1, 1);

    private IList<TagResponse>? _tags;
    private DateTime _tagsExpiresUtc = DateTime.MinValue;

    private IList<TagOptionListResponse>? _optionLists;
    private DateTime _optionListsExpiresUtc = DateTime.MinValue;

    public SharedDataCache(IApiClientProvider apiClientProvider, ILogger<SharedDataCache> logger)
    {
        _apiClientProvider = apiClientProvider;
        _logger = logger;
    }

    public async Task<IList<TagResponse>> GetTagsAsync(CancellationToken ct = default)
    {
        if (_tags is not null && DateTime.UtcNow < _tagsExpiresUtc)
        {
            return _tags;
        }

        await _tagGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock
            if (_tags is not null && DateTime.UtcNow < _tagsExpiresUtc)
            {
                return _tags;
            }

            _logger.LogDebug("SharedDataCache: Fetching tags from API");
            _tags = await _apiClientProvider.Activity.GetTags().ConfigureAwait(false);
            _tagsExpiresUtc = DateTime.UtcNow.Add(CacheTtl);

            return _tags;
        }
        finally
        {
            _tagGate.Release();
        }
    }

    public async Task<IList<TagOptionListResponse>> GetOptionListsAsync(CancellationToken ct = default)
    {
        if (_optionLists is not null && DateTime.UtcNow < _optionListsExpiresUtc)
        {
            return _optionLists;
        }

        await _optionListGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_optionLists is not null && DateTime.UtcNow < _optionListsExpiresUtc)
            {
                return _optionLists;
            }

            _logger.LogDebug("SharedDataCache: Fetching option lists from API");
            _optionLists = await _apiClientProvider.Activity.GetTagOptionListsAsync().ConfigureAwait(false);
            _optionListsExpiresUtc = DateTime.UtcNow.Add(CacheTtl);

            return _optionLists;
        }
        finally
        {
            _optionListGate.Release();
        }
    }

    public void SeedTags(IList<TagResponse> tags)
    {
        _tags = tags;
        _tagsExpiresUtc = DateTime.UtcNow.Add(CacheTtl);
        _logger.LogDebug("SharedDataCache: Tags seeded from external source ({Count} items)", tags.Count);
    }

    public void Invalidate()
    {
        _tags = null;
        _tagsExpiresUtc = DateTime.MinValue;
        _optionLists = null;
        _optionListsExpiresUtc = DateTime.MinValue;
        _logger.LogDebug("SharedDataCache: Cache invalidated");
    }
}
