using System;
using System.IO;
using System.Net.Mime;
using Jellyfin.Plugin.Letterboxd.Letterboxd;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Letterboxd.Api;

/// <summary>
/// REST endpoints backing the web UI badge: the injected client script and a
/// per-item rating lookup against the plugin's local cache.
/// </summary>
[ApiController]
[Route("LetterboxdRatings")]
public class LetterboxdController : ControllerBase
{
    private readonly RatingCache _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="LetterboxdController"/> class.
    /// </summary>
    /// <param name="cache">The rating cache.</param>
    public LetterboxdController(RatingCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Serves the client-side script that renders the Letterboxd badge on
    /// item detail pages. Anonymous because it is loaded via a plain
    /// &lt;script&gt; tag from index.html, before authentication happens.
    /// </summary>
    /// <returns>The JavaScript file.</returns>
    [HttpGet("ClientScript")]
    [AllowAnonymous]
    [Produces("application/javascript")]
    public ActionResult GetClientScript()
    {
        var assembly = typeof(LetterboxdController).Assembly;
        var name = typeof(Plugin).Namespace + ".Web.clientScript.js";
        var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(stream, "application/javascript");
    }

    /// <summary>
    /// Returns the cached Letterboxd rating for a library item.
    /// </summary>
    /// <param name="itemId">The library item ID.</param>
    /// <returns>The rating, or 404 if none has been fetched for this item.</returns>
    [HttpGet("Rating/{itemId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LetterboxdRating> GetRating([FromRoute] Guid itemId)
    {
        if (_cache.TryGet(itemId, out var rating) && rating is not null)
        {
            return Ok(rating);
        }

        return NotFound();
    }
}
