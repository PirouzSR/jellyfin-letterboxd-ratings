using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Letterboxd.Web;

/// <summary>
/// Injects the Letterboxd badge client script into jellyfin-web's index.html
/// at server startup. The injection is idempotent (guarded by a marker) and
/// self-healing: because it runs on every startup, it survives jellyfin-web
/// updates that replace index.html (e.g. Docker image upgrades on TrueNAS).
/// </summary>
public partial class WebInterfaceInjector : IHostedService
{
    // Relative URL so it works behind reverse proxies with a path prefix.
    private const string ScriptTag =
        "<script defer src=\"LetterboxdRatings/ClientScript\" data-letterboxd-ratings=\"injected\"></script>";

    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<WebInterfaceInjector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebInterfaceInjector"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{WebInterfaceInjector}"/> interface.</param>
    public WebInterfaceInjector(IApplicationPaths applicationPaths, ILogger<WebInterfaceInjector> logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    [GeneratedRegex("""<script[^>]*data-letterboxd-ratings[^>]*>\s*</script>\n?""")]
    private static partial Regex ExistingTagRegex();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var webPath = _applicationPaths.WebPath;
            if (string.IsNullOrEmpty(webPath))
            {
                _logger.LogWarning("Letterboxd: web client path unknown; badge script not injected. "
                                   + "The description-line rating still works.");
                return Task.CompletedTask;
            }

            var indexPath = Path.Combine(webPath, "index.html");
            if (!File.Exists(indexPath))
            {
                _logger.LogWarning("Letterboxd: {Path} not found; badge script not injected. "
                                   + "This is expected when jellyfin-web is hosted separately.", indexPath);
                return Task.CompletedTask;
            }

            var html = File.ReadAllText(indexPath);

            // Replace any previously injected tag (possibly from an older
            // plugin version), then insert the current one before </body>.
            var cleaned = ExistingTagRegex().Replace(html, string.Empty);

            var bodyClose = cleaned.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (bodyClose < 0)
            {
                _logger.LogWarning("Letterboxd: could not find </body> in index.html; badge script not injected.");
                return Task.CompletedTask;
            }

            var updated = cleaned.Insert(bodyClose, ScriptTag + "\n");
            if (!string.Equals(updated, html, StringComparison.Ordinal))
            {
                File.WriteAllText(indexPath, updated);
                _logger.LogInformation("Letterboxd: badge script injected into {Path}", indexPath);
            }
            else
            {
                _logger.LogDebug("Letterboxd: badge script already present in index.html");
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                ex,
                "Letterboxd: no write permission for the web client directory, so the badge script was not "
                + "injected. The description-line rating still works. To get the badge, make the jellyfin-web "
                + "directory writable by the Jellyfin user, or add this tag to index.html manually before "
                + "</body>: {Tag}",
                ScriptTag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Letterboxd: unexpected error injecting badge script");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // The tag is intentionally left in place on shutdown; it is harmless
        // when the plugin is absent (the script endpoint just 404s) and is
        // cleaned/re-written on the next startup.
        return Task.CompletedTask;
    }
}
