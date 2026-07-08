using System;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Letterboxd.Letterboxd;

/// <summary>
/// Parses a Letterboxd film page and extracts the community rating.
/// Primary source is the schema.org JSON-LD block embedded in every film page;
/// the twitter:data2 meta tag is used as a fallback.
/// </summary>
public static partial class LetterboxdPageParser
{
    [GeneratedRegex(
        """<script\s+type="application/ld\+json">(.*?)</script>""",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex JsonLdRegex();

    [GeneratedRegex(
        """<meta\s+name="twitter:data2"\s+content="([\d.]+)\s+out\s+of\s+5""",
        RegexOptions.IgnoreCase)]
    private static partial Regex TwitterMetaRegex();

    /// <summary>
    /// Attempts to extract the aggregate rating from a Letterboxd film page.
    /// </summary>
    /// <param name="html">The raw HTML of the film page.</param>
    /// <returns>The parsed rating, or <c>null</c> if no rating is present
    /// (e.g. obscure films with too few ratings have no aggregateRating).</returns>
    public static LetterboxdRating? Parse(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }

        var rating = ParseJsonLd(html);
        return rating ?? ParseTwitterMeta(html);
    }

    private static LetterboxdRating? ParseJsonLd(string html)
    {
        var match = JsonLdRegex().Match(html);
        if (!match.Success)
        {
            return null;
        }

        // Letterboxd wraps the JSON in CDATA comment guards:
        //   /* <![CDATA[ */ { ... } /* ]]> */
        var payload = match.Groups[1].Value
            .Replace("/* <![CDATA[ */", string.Empty, StringComparison.Ordinal)
            .Replace("/* ]]> */", string.Empty, StringComparison.Ordinal)
            .Trim();

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("aggregateRating", out var agg))
            {
                return null;
            }

            if (!agg.TryGetProperty("ratingValue", out var valueElement))
            {
                return null;
            }

            double value = valueElement.ValueKind switch
            {
                JsonValueKind.Number => valueElement.GetDouble(),
                JsonValueKind.String when double.TryParse(
                    valueElement.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
                _ => double.NaN
            };

            if (double.IsNaN(value) || value <= 0 || value > 5)
            {
                return null;
            }

            long count = 0;
            if (agg.TryGetProperty("ratingCount", out var countElement)
                && countElement.ValueKind == JsonValueKind.Number)
            {
                count = countElement.GetInt64();
            }

            string? url = null;
            if (root.TryGetProperty("url", out var urlElement)
                && urlElement.ValueKind == JsonValueKind.String)
            {
                url = urlElement.GetString();
            }

            return new LetterboxdRating { Value = value, Count = count, Url = url };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static LetterboxdRating? ParseTwitterMeta(string html)
    {
        var match = TwitterMetaRegex().Match(html);
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(
                match.Groups[1].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value)
            || value <= 0
            || value > 5)
        {
            return null;
        }

        return new LetterboxdRating { Value = value, Count = 0 };
    }
}
