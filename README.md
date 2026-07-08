# Jellyfin Letterboxd Ratings Plugin

Shows the **Letterboxd community star rating** (e.g. `★ 4.1/5 on Letterboxd (108,132 ratings)`) on every movie in your Jellyfin library, right alongside your existing IMDb / Metacritic metadata.

## How it works

Letterboxd's official API is invite-only and they explicitly do not grant access for personal projects, so this plugin doesn't use it. Instead it uses Letterboxd's **officially documented TMDB redirect** (`https://letterboxd.com/tmdb/{id}` forwards to the film's page) and reads the average rating from the structured `schema.org` JSON-LD metadata that Letterboxd embeds in every film page for search engines. No API key, no account, no third-party aggregator.

A scheduled task (**Dashboard → Scheduled Tasks → Letterboxd → Update Letterboxd Ratings**, weekly by default):

1. Lists every movie in your library.
2. Skips movies checked recently (14-day cache, configurable) and movies with no TMDB/IMDb ID.
3. Resolves each remaining movie to its Letterboxd page (TMDB ID first, IMDb fallback) with a polite delay between requests (1.5 s default).
4. Parses the star rating and rating count, and writes them to the fields you've enabled.

## The Letterboxd badge (logo + rating in the ratings row)

Jellyfin's server plugins can't natively add a third badge to the web UI, so this plugin uses the same technique as Intro Skipper and Jellyfin Enhanced: at server startup it injects a small script tag into jellyfin-web's `index.html`. The script watches for movie detail pages and inserts a badge — the Letterboxd three-dot logo followed by the star value (e.g. `⬤⬤⬤ 4.1`) — directly beside the IMDb star and critic badges in the media info row. Clicking the badge opens the film's Letterboxd page.

- The injection is idempotent and **self-healing**: it re-runs on every server start, so it survives Docker image updates that replace `index.html`.
- If the web directory isn't writable (check the log for a `Letterboxd:` warning), the badge is skipped but everything else still works; the log message includes the one-line tag you can add to `index.html` manually.
- Ratings shown by the badge come from the plugin's local cache, so run the scheduled task at least once first.

## Client compatibility

| Client | What you see | How |
|---|---|---|
| **Web UI** (browser, desktop app, clients that embed jellyfin-web) | Letterboxd logo badge in the ratings row, next to IMDb/critic badges. The description line is hidden client-side to avoid duplication. | Injected script |
| **Google TV / Android TV, Roku, Swiftfin, Kodi, and all other native apps** | The rating as the last line of the movie description, e.g. `★ 4.1/5 on Letterboxd (108,132 ratings)` | Server metadata — rendered natively by every client with zero client-side changes |

Native apps are compiled clients that render their own hardcoded UI from the server's metadata API; **no Jellyfin plugin can inject custom UI into them** (this is why even Intro Skipper required native clients to add built-in support). The description line is the designed failover: it travels through the same metadata API as everything else, so it appears automatically and reliably on every device, and each client shows the rating exactly once.

**Roku note:** if the ★ glyph renders as a box on your Roku's font, change the description format in plugin settings to a glyph-free version such as `Letterboxd: {rating}/5 ({count} ratings)` — formats starting with "Letterboxd" are fully supported by the updater.

## Display options (plugin settings)

| Option | Default | What it does |
|---|---|---|
| **Append to description** | ✅ on | Adds `★ 4.1/5 on Letterboxd (…)` as the last line of the movie overview. Works in every client, never conflicts with IMDb/Metacritic. Idempotent — re-runs update the line in place. |
| **Write to Critic Rating** | off | Converts to 0–100 and fills the critic badge in the ratings row. Only enable if you don't use Rotten Tomatoes/OMDb (same badge). |
| **Overwrite Community Rating** | off | Replaces the IMDb star badge with Letterboxd × 2 (10-point scale). |

> The injected badge covers the web UI (and clients that embed it). The description line is the fallback that works in *every* client (Android TV, Swiftfin, Kodi, etc.), which is why both are on by default.

## Requirements

- **Jellyfin 10.11.x** (built against `targetAbi 10.11.0.0`, .NET 9).
- Movies must have a TMDB or IMDb ID (they will if TMDB metadata is enabled, which is the default).

### Running Jellyfin 10.10.x?

Two small changes, then rebuild:
1. `Jellyfin.Plugin.Letterboxd.csproj`: `net9.0` → `net8.0`, package versions `10.11.*` → `10.10.*`.
2. `ScheduledTasks/UpdateLetterboxdRatingsTask.cs`: 10.10 uses string trigger constants instead of the enum — replace `Type = TaskTriggerInfoType.IntervalTrigger` with `Type = TaskTriggerInfo.TriggerInterval`.
Note 10.10 also uses `Taglines` (array) instead of `Tagline`, but this plugin doesn't touch taglines so no other changes are needed.

## Installing via the Jellyfin plugin catalog (recommended)

This repo doubles as a Jellyfin **plugin repository**. Once it's on GitHub and you've pushed a version tag (e.g. `v1.0.0.0`), CI publishes a release zip and maintains `manifest.json` automatically. Then in Jellyfin:

1. **Dashboard → Plugins → Repositories → +**
2. Name: `Letterboxd Ratings`, URL:
   `https://raw.githubusercontent.com/<your-username>/jellyfin-plugin-letterboxd/main/manifest.json`
3. **Catalog** tab → *Letterboxd Ratings* appears under Metadata → Install → restart Jellyfin.

Updates are then delivered through the normal plugin update mechanism whenever you push a new `v*` tag.

## Building

```bash
dotnet build --configuration Release
# output: Jellyfin.Plugin.Letterboxd/bin/Release/net9.0/Jellyfin.Plugin.Letterboxd.dll
```

Or push this repo to GitHub — the included Actions workflow builds, runs the unit tests, and attaches `letterboxd-ratings.zip` as an artifact (and as a Release if you push a `v*` tag).

## Installing on TrueNAS SCALE

1. Build the DLL (above) or grab it from your GitHub Actions artifact.
2. Find your Jellyfin app's **config** dataset/host path (the one you chose for "Jellyfin Config Storage" when installing the app — e.g. `/mnt/tank/apps/jellyfin/config`).
3. From the TrueNAS shell:
   ```bash
   mkdir -p /mnt/<pool>/<...>/config/plugins/LetterboxdRatings_1.0.0.0
   # copy Jellyfin.Plugin.Letterboxd.dll into that folder
   chown -R 568:568 /mnt/<pool>/<...>/config/plugins/LetterboxdRatings_1.0.0.0   # apps user, if your app runs as default
   ```
4. Restart the Jellyfin app.
5. Verify under **Dashboard → Plugins → My Plugins** that *Letterboxd Ratings* shows as Active, then open its settings.
6. Run **Dashboard → Scheduled Tasks → Update Letterboxd Ratings** manually the first time. For a 1,000-movie library at the default 1.5 s delay the first run takes ~25–30 minutes; later runs only fetch stale items.

## Troubleshooting

- **Nothing appears after the task runs** — check the Jellyfin log for lines starting with `Letterboxd:`. The summary line reports how many items were updated / skipped / not found.
- **`Letterboxd rejected the request (403)`** — Letterboxd sits behind Cloudflare, which occasionally blocks non-browser TLS fingerprints or aggressive request rates. Increase the request delay, and/or update the User-Agent in plugin settings to match a current browser. Failures are cached-free: the task will retry those items next run.
- **A movie is skipped with "no TMDB or IMDb ID"** — refresh its metadata so an ID gets assigned, or add one manually in the item's metadata editor (External IDs).
- **Wrong film matched** — Letterboxd resolves by TMDB ID, so fix the TMDB ID on the Jellyfin item and re-run.
- **Remove the ratings** — disable "Append to description", then refresh metadata for the library (the plugin only ever adds/replaces its own `★ … Letterboxd …` line, so a metadata refresh from your providers restores the original overview).

## Notes on politeness & ToS

This fetches only public film pages, at a slow configurable rate (default one request per 1.5 s), with local caching so each film is hit at most once per two weeks. Keep the delay reasonable; it's your responsibility to use this in line with Letterboxd's terms.

## Project layout

```
Jellyfin.Plugin.Letterboxd/
├── Plugin.cs                          # plugin entry point + config page registration
├── ServiceRegistrator.cs              # DI registrations
├── Configuration/
│   ├── PluginConfiguration.cs         # settings model
│   └── configPage.html                # dashboard settings UI (embedded resource)
├── Letterboxd/
│   ├── LetterboxdClient.cs            # HTTP: tmdb/imdb redirect → film page
│   ├── LetterboxdPageParser.cs        # JSON-LD + twitter:data2 parsing (pure, tested)
│   ├── LetterboxdRating.cs            # rating model
│   └── RatingCache.cs                 # persistent per-item freshness cache
├── Api/
│   └── LetterboxdController.cs        # REST: serves client.js + cached ratings
├── Web/
│   ├── clientScript.js                # the badge renderer (embedded resource)
│   └── WebInterfaceInjector.cs        # injects the script tag at startup
└── ScheduledTasks/
    └── UpdateLetterboxdRatingsTask.cs # the weekly task
Jellyfin.Plugin.Letterboxd.Tests/      # xunit tests for the parser
.github/update_manifest.py             # maintains manifest.json on releases
```
