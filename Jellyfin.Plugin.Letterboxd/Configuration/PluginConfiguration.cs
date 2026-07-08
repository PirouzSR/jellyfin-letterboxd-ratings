using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Letterboxd.Configuration;

/// <summary>
/// Plugin configuration for Letterboxd Ratings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the Letterboxd rating is appended
    /// as a line at the end of the movie's overview (description). This is the
    /// most visible placement across all Jellyfin clients.
    /// </summary>
    public bool WriteToOverview { get; set; } = true;

    /// <summary>
    /// Gets or sets the format used for the overview line.
    /// Placeholders: {rating} = 0.5-5 star value, {count} = number of ratings.
    /// </summary>
    public string OverviewFormat { get; set; } = "★ {rating}/5 on Letterboxd ({count} ratings)";

    /// <summary>
    /// Gets or sets a value indicating whether the Letterboxd rating (converted
    /// to a 0-100 scale) is written to the Critic Rating field. Enable this only
    /// if you do not already use Rotten Tomatoes / OMDb critic scores, because it
    /// occupies the same badge in the ratings row.
    /// </summary>
    public bool WriteToCriticRating { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Letterboxd rating (converted
    /// to a 0-10 scale) overwrites the Community Rating (the star badge, normally
    /// populated by IMDb/TMDb). Off by default so your IMDb stars are preserved.
    /// </summary>
    public bool OverwriteCommunityRating { get; set; }

    /// <summary>
    /// Gets or sets the delay in milliseconds between requests to Letterboxd.
    /// Be polite: do not set this below 500ms.
    /// </summary>
    public int RequestDelayMs { get; set; } = 1500;

    /// <summary>
    /// Gets or sets the number of days a fetched rating is considered fresh.
    /// Items refreshed within this window are skipped by the scheduled task.
    /// </summary>
    public int RefreshIntervalDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets the User-Agent header sent to Letterboxd. A realistic
    /// browser UA avoids being rejected by the CDN.
    /// </summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";
}
