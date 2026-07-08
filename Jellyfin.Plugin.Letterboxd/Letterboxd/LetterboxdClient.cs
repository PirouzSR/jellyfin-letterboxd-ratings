using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Letterboxd.Letterboxd;

/// <summary>
/// Fetches community ratings from Letterboxd film pages.
/// Uses the documented https://letterboxd.com/tmdb/{id} redirect to resolve a
/// TMDB movie ID to its Letterboxd film page, then parses the page's JSON-LD.
/// </summary>
public class LetterboxdClient
{
    private const string BaseUrl = "https://letterboxd.com";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LetterboxdClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LetterboxdClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{LetterboxdClient}"/> interface.</param>
    public LetterboxdClient(IHttpClientFactory httpClientFactory, ILogger<LetterboxdClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the Letterboxd rating for a movie by TMDB ID, falling back to IMDb ID.
    /// </summary>
    /// <param name="tmdbId">The TMDB movie ID, if known.</param>
    /// <param name="imdbId">The IMDb ID (ttXXXXXXX), if known.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rating, or <c>null</c> if the film could not be resolved or has no rating.</returns>
    public async Task<LetterboxdRating?> GetRatingAsync(
        string? tmdbId,
        string? imdbId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(tmdbId))
        {
            var rating = await FetchAsync(
                string.Format(CultureInfo.InvariantCulture, "{0}/tmdb/{1}/", BaseUrl, tmdbId.Trim()),
                cancellationToken).ConfigureAwait(false);
            if (rating is not null)
            {
                return rating;
            }
        }

        if (!string.IsNullOrWhiteSpace(imdbId))
        {
            return await FetchAsync(
                string.Format(CultureInfo.InvariantCulture, "{0}/imdb/{1}/", BaseUrl, imdbId.Trim()),
                cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task<LetterboxdRating?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration;

        try
        {
            var client = _httpClientFactory.CreateClient(NamedClient.Default);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(config?.UserAgent))
            {
                request.Headers.UserAgent.Clear();
                request.Headers.TryAddWithoutValidation("User-Agent", config.UserAgent);
            }

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));

            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Letterboxd has no film page for {Url}", url);
                return null;
            }

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning(
                    "Letterboxd rejected the request ({Status}) for {Url}. "
                    + "You may be rate-limited; increase the request delay in plugin settings.",
                    (int)response.StatusCode,
                    url);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var rating = LetterboxdPageParser.Parse(html);

            if (rating is not null && string.IsNullOrEmpty(rating.Url))
            {
                rating.Url = response.RequestMessage?.RequestUri?.ToString();
            }

            return rating;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error fetching Letterboxd page {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Letterboxd page {Url}", url);
            return null;
        }
    }
}
