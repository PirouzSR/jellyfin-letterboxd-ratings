/* Letterboxd Ratings — Jellyfin web client badge
 *
 * Injected into index.html by the plugin at server startup. Watches for item
 * detail pages and inserts a Letterboxd badge (three-dot logo + star value)
 * into the media info row, right beside the community (IMDb) star and the
 * critic rating badges. Rating data comes from the plugin's local cache via
 * GET LetterboxdRatings/Rating/{itemId}.
 */
(function () {
    'use strict';

    var BADGE_CLASS = 'letterboxdRatingBadge';
    var ratingCache = {}; // itemId -> rating object | null (null = known absent)
    var lastRenderedFor = null;

    // Letterboxd's three-dot mark, drawn inline (orange / green / blue).
    var LOGO_SVG =
        '<svg viewBox="0 0 500 250" style="width:1.4em;height:.7em;vertical-align:-5%;margin-right:.3em;" aria-hidden="true">' +
        '<circle cx="125" cy="125" r="120" fill="#ff8000"/>' +
        '<circle cx="375" cy="125" r="120" fill="#40bcf4"/>' +
        '<circle cx="250" cy="125" r="120" fill="#00e054"/>' +
        '</svg>';

    function getApiClient() {
        return window.ApiClient || null;
    }

    function getItemIdFromLocation() {
        // Detail routes look like  #/details?id=<guid>&serverId=...
        var hash = window.location.hash || '';
        var queryIndex = hash.indexOf('?');
        var source = queryIndex >= 0 ? hash.substring(queryIndex + 1) : window.location.search.replace(/^\?/, '');
        if (hash.indexOf('details') === -1 && window.location.pathname.indexOf('details') === -1) {
            return null;
        }
        var params = new URLSearchParams(source);
        var id = params.get('id');
        return id && /^[0-9a-fA-F-]{32,36}$/.test(id) ? id : null;
    }

    function fetchRating(itemId) {
        if (Object.prototype.hasOwnProperty.call(ratingCache, itemId)) {
            return Promise.resolve(ratingCache[itemId]);
        }
        var apiClient = getApiClient();
        if (!apiClient) {
            return Promise.resolve(null);
        }
        return apiClient
            .getJSON(apiClient.getUrl('LetterboxdRatings/Rating/' + itemId))
            .then(function (rating) {
                ratingCache[itemId] = rating || null;
                return ratingCache[itemId];
            })
            .catch(function () {
                ratingCache[itemId] = null;
                return null;
            });
    }

    function buildBadge(rating) {
        var badge = document.createElement('div');
        badge.className = 'mediaInfoItem ' + BADGE_CLASS;
        badge.title = 'Letterboxd average rating'
            + (rating.Count ? ' (' + Number(rating.Count).toLocaleString() + ' ratings)' : '');
        badge.style.display = 'inline-flex';
        badge.style.alignItems = 'center';
        badge.innerHTML = LOGO_SVG
            + '<span>' + Number(rating.Value).toFixed(1) + '</span>';
        if (rating.Url) {
            badge.style.cursor = 'pointer';
            badge.addEventListener('click', function () {
                window.open(rating.Url, '_blank', 'noopener');
            });
        }
        return badge;
    }

    function insertBadges(rating) {
        // A details page can contain more than one visible itemMiscInfo block
        // (e.g. primary + repeated in some layouts); handle each once.
        var containers = document.querySelectorAll('.itemMiscInfo-primary');
        for (var i = 0; i < containers.length; i++) {
            var container = containers[i];
            if (container.querySelector('.' + BADGE_CLASS)) {
                continue;
            }

            var badge = buildBadge(rating);

            // Preferred position: immediately after the critic rating badge,
            // else after the community star rating, else append to the row.
            var anchor = container.querySelector('.mediaInfoCriticRating')
                || container.querySelector('.starRatingContainer');
            if (anchor && anchor.parentNode === container) {
                anchor.insertAdjacentElement('afterend', badge);
            } else {
                container.appendChild(badge);
            }
        }
    }

    function removeBadges() {
        var existing = document.querySelectorAll('.' + BADGE_CLASS);
        for (var i = 0; i < existing.length; i++) {
            existing[i].remove();
        }
    }

    function update() {
        var itemId = getItemIdFromLocation();
        if (!itemId) {
            lastRenderedFor = null;
            return;
        }

        // If we navigated to a different item, drop stale badges.
        if (lastRenderedFor && lastRenderedFor !== itemId) {
            removeBadges();
        }

        fetchRating(itemId).then(function (rating) {
            // Re-check: the user may have navigated away while fetching.
            if (getItemIdFromLocation() !== itemId) {
                return;
            }
            if (rating && rating.Value > 0) {
                insertBadges(rating);
                lastRenderedFor = itemId;
            }
        });
    }

    function start() {
        // jellyfin-web is a SPA; watch for route + DOM changes.
        var scheduled = false;
        var observer = new MutationObserver(function () {
            if (scheduled) {
                return;
            }
            scheduled = true;
            setTimeout(function () {
                scheduled = false;
                update();
            }, 250);
        });
        observer.observe(document.body, { childList: true, subtree: true });

        window.addEventListener('hashchange', update);
        document.addEventListener('viewshow', update);
        update();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
