namespace Jellyfin.Plugin.Letterboxd.Letterboxd;

/// <summary>
/// A Letterboxd community rating for a film.
/// </summary>
public class LetterboxdRating
{
    /// <summary>
    /// Gets or sets the weighted average rating on Letterboxd's 0.5-5 star scale.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Gets or sets the number of member ratings contributing to the average.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the canonical Letterboxd film page URL.
    /// </summary>
    public string? Url { get; set; }
}
