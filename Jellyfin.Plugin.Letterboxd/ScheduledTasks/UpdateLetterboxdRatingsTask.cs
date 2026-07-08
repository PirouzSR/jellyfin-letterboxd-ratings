using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Letterboxd.Configuration;
using Jellyfin.Plugin.Letterboxd.Letterboxd;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Letterboxd.ScheduledTasks;

/// <summary>
/// Scheduled task that fetches Letterboxd community ratings for every movie in
/// the library and writes them to the configured metadata fields.
/// </summary>
public partial class UpdateLetterboxdRatingsTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly LetterboxdClient _client;
    private readonly RatingCache _cache;
    private readonly ILogger<UpdateLetterboxdRatingsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateLetterboxdRatingsTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="client">The Letterboxd client.</param>
    /// <param name="cache">The rating cache.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{UpdateLetterboxdRatingsTask}"/> interface.</param>
    public UpdateLetterboxdRatingsTask(
        ILibraryManager libraryManager,
        LetterboxdClient client,
        RatingCache cache,
        ILogger<UpdateLetterboxdRatingsTask> logger)
    {
        _libraryManager = libraryManager;
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Update Letterboxd Ratings";

    /// <inheritdoc />
    public string Key => "LetterboxdRatingsUpdate";

    /// <inheritdoc />
    public string Description => "Fetches Letterboxd community star ratings for movies in the library.";

    /// <inheritdoc />
    public string Category => "Letterboxd";

    // Matches a rating line previously written by this plugin so it can be
    // replaced idempotently on refresh: a full line that contains the word
    // Letterboxd and starts with either the star glyph or "Letterboxd".
    [GeneratedRegex(@"\n*^(?:★.*Letterboxd.*|Letterboxd\b.*)$", RegexOptions.Multiline)]
    private static partial Regex OverviewLineRegex();

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.IntervalTrigger,
                IntervalTicks = TimeSpan.FromDays(7).Ticks
            }
        ];
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();

        var movies = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie],
            IsVirtualItem = false,
            Recursive = true
        });

        _logger.LogInformation("Letterboxd: scanning {Count} movies", movies.Count);

        var refreshWindow = TimeSpan.FromDays(Math.Max(1, config.RefreshIntervalDays));
        var delay = TimeSpan.FromMilliseconds(Math.Max(500, config.RequestDelayMs));

        var updated = 0;
        var skipped = 0;
        var notFound = 0;
        var processed = 0;

        try
        {
            foreach (var item in movies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processed++;
                progress.Report(100.0 * processed / Math.Max(1, movies.Count));

                if (_cache.IsFresh(item.Id, refreshWindow))
                {
                    skipped++;
                    continue;
                }

                var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);
                var imdbId = item.GetProviderId(MetadataProvider.Imdb);

                if (string.IsNullOrWhiteSpace(tmdbId) && string.IsNullOrWhiteSpace(imdbId))
                {
                    _logger.LogDebug("Letterboxd: '{Name}' has no TMDB or IMDb ID; skipping", item.Name);
                    skipped++;
                    continue;
                }

                var rating = await _client.GetRatingAsync(tmdbId, imdbId, cancellationToken)
                    .ConfigureAwait(false);

                _cache.Record(item.Id, rating);

                if (rating is null)
                {
                    notFound++;
                }
                else
                {
                    var changed = ApplyRating(item, rating, config);
                    if (changed)
                    {
                        await _libraryManager
                            .UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken)
                            .ConfigureAwait(false);
                        updated++;
                        _logger.LogDebug(
                            "Letterboxd: '{Name}' -> {Rating}/5 ({Count} ratings)",
                            item.Name,
                            rating.Value,
                            rating.Count);
                    }
                }

                // Be polite to Letterboxd between network requests.
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _cache.Save();
        }

        _logger.LogInformation(
            "Letterboxd: done. {Updated} updated, {Skipped} skipped (fresh/no IDs), {NotFound} not found on Letterboxd",
            updated,
            skipped,
            notFound);
    }

    private static bool ApplyRating(BaseItem item, LetterboxdRating rating, PluginConfiguration config)
    {
        var changed = false;

        if (config.WriteToOverview)
        {
            var line = BuildOverviewLine(rating, config);
            var overview = item.Overview ?? string.Empty;

            // Remove any line this plugin wrote previously, then append the new one.
            var stripped = OverviewLineRegex().Replace(overview, string.Empty).TrimEnd();
            var newOverview = string.IsNullOrEmpty(stripped) ? line : stripped + "\n\n" + line;

            if (!string.Equals(overview, newOverview, StringComparison.Ordinal))
            {
                item.Overview = newOverview;
                changed = true;
            }
        }

        if (config.WriteToCriticRating)
        {
            var critic = (float)Math.Round(rating.Value * 20.0, 0);
            if (item.CriticRating != critic)
            {
                item.CriticRating = critic;
                changed = true;
            }
        }

        if (config.OverwriteCommunityRating)
        {
            var community = (float)Math.Round(rating.Value * 2.0, 1);
            if (item.CommunityRating != community)
            {
                item.CommunityRating = community;
                changed = true;
            }
        }

        return changed;
    }

    private static string BuildOverviewLine(LetterboxdRating rating, PluginConfiguration config)
    {
        var format = string.IsNullOrWhiteSpace(config.OverviewFormat)
            ? "★ {rating}/5 on Letterboxd ({count} ratings)"
            : config.OverviewFormat;

        var line = format
            .Replace(
                "{rating}",
                rating.Value.ToString("0.0#", CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "{count}",
                rating.Count.ToString("N0", CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase);

        // Guarantee the line matches the idempotency regex (starts with ★ or
        // "Letterboxd") so it can be found and replaced on refresh. Formats
        // beginning with "Letterboxd" are left untouched to stay safe on
        // clients whose fonts may not include the ★ glyph (e.g. Roku).
        if (!line.StartsWith('★') && !line.StartsWith("Letterboxd", StringComparison.Ordinal))
        {
            line = "★ " + line;
        }

        if (!line.Contains("Letterboxd", StringComparison.OrdinalIgnoreCase))
        {
            line += " — Letterboxd";
        }

        return line;
    }
}
