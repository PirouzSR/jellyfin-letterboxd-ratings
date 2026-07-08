using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Letterboxd.Letterboxd;

/// <summary>
/// A small persistent cache recording when each library item was last checked
/// against Letterboxd, so the scheduled task only re-fetches stale items and
/// does not hammer Letterboxd for films it already knows are missing.
/// </summary>
public class RatingCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _cacheFilePath;
    private readonly ILogger<RatingCache> _logger;
    private readonly object _saveLock = new();

    private ConcurrentDictionary<Guid, CacheEntry> _entries = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingCache"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{RatingCache}"/> interface.</param>
    public RatingCache(IApplicationPaths applicationPaths, ILogger<RatingCache> logger)
    {
        _logger = logger;
        var dir = Path.Combine(applicationPaths.CachePath, "letterboxd-ratings");
        Directory.CreateDirectory(dir);
        _cacheFilePath = Path.Combine(dir, "cache.json");
        Load();
    }

    /// <summary>
    /// Returns whether the given item was checked within the freshness window.
    /// </summary>
    /// <param name="itemId">The library item ID.</param>
    /// <param name="maxAge">The freshness window.</param>
    /// <returns><c>true</c> if the item is fresh and can be skipped.</returns>
    public bool IsFresh(Guid itemId, TimeSpan maxAge)
    {
        return _entries.TryGetValue(itemId, out var entry)
               && DateTime.UtcNow - entry.CheckedUtc < maxAge;
    }

    /// <summary>
    /// Records the result of a fetch for an item.
    /// </summary>
    /// <param name="itemId">The library item ID.</param>
    /// <param name="rating">The fetched rating, or <c>null</c> if none was found.</param>
    public void Record(Guid itemId, LetterboxdRating? rating)
    {
        _entries[itemId] = new CacheEntry
        {
            CheckedUtc = DateTime.UtcNow,
            Found = rating is not null,
            Value = rating?.Value ?? 0,
            Count = rating?.Count ?? 0,
            Url = rating?.Url
        };
    }

    /// <summary>
    /// Attempts to retrieve a previously fetched rating for an item.
    /// </summary>
    /// <param name="itemId">The library item ID.</param>
    /// <param name="rating">The cached rating, if one was found for the item.</param>
    /// <returns><c>true</c> if a rating exists in the cache for this item.</returns>
    public bool TryGet(Guid itemId, out LetterboxdRating? rating)
    {
        if (_entries.TryGetValue(itemId, out var entry) && entry.Found && entry.Value > 0)
        {
            rating = new LetterboxdRating
            {
                Value = entry.Value,
                Count = entry.Count,
                Url = entry.Url
            };
            return true;
        }

        rating = null;
        return false;
    }

    /// <summary>
    /// Persists the cache to disk.
    /// </summary>
    public void Save()
    {
        lock (_saveLock)
        {
            try
            {
                var tmp = _cacheFilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, JsonOptions));
                File.Move(tmp, _cacheFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save Letterboxd rating cache to {Path}", _cacheFilePath);
            }
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = File.ReadAllText(_cacheFilePath);
                var loaded = JsonSerializer.Deserialize<ConcurrentDictionary<Guid, CacheEntry>>(json);
                if (loaded is not null)
                {
                    _entries = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Letterboxd rating cache; starting fresh");
            _entries = new ConcurrentDictionary<Guid, CacheEntry>();
        }
    }

    private sealed class CacheEntry
    {
        public DateTime CheckedUtc { get; set; }

        public bool Found { get; set; }

        public double Value { get; set; }

        public long Count { get; set; }

        public string? Url { get; set; }
    }
}
