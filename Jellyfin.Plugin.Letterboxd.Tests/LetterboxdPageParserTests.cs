using Jellyfin.Plugin.Letterboxd.Letterboxd;
using Xunit;

namespace Jellyfin.Plugin.Letterboxd.Tests;

public class LetterboxdPageParserTests
{
    private const string JsonLdPage = """
        <html><head>
        <script type="application/ld+json">
        /* <![CDATA[ */
        {
          "@type": "Movie",
          "name": "Parasite",
          "url": "https://letterboxd.com/film/parasite-2019/",
          "aggregateRating": {
            "@type": "AggregateRating",
            "ratingValue": 4.56,
            "ratingCount": 1083920,
            "reviewCount": 128733,
            "bestRating": 5,
            "worstRating": 0.5
          }
        }
        /* ]]> */
        </script>
        </head><body></body></html>
        """;

    private const string TwitterMetaPage = """
        <html><head>
        <meta name="twitter:data1" content="Some Film" />
        <meta name="twitter:data2" content="4.06 out of 5" />
        </head><body></body></html>
        """;

    private const string NoRatingPage = """
        <html><head>
        <script type="application/ld+json">
        /* <![CDATA[ */
        { "@type": "Movie", "name": "Obscure Short", "url": "https://letterboxd.com/film/obscure/" }
        /* ]]> */
        </script>
        </head><body></body></html>
        """;

    [Fact]
    public void Parse_JsonLd_ReturnsRatingCountAndUrl()
    {
        var rating = LetterboxdPageParser.Parse(JsonLdPage);

        Assert.NotNull(rating);
        Assert.Equal(4.56, rating!.Value, precision: 2);
        Assert.Equal(1083920, rating.Count);
        Assert.Equal("https://letterboxd.com/film/parasite-2019/", rating.Url);
    }

    [Fact]
    public void Parse_TwitterMetaFallback_ReturnsRating()
    {
        var rating = LetterboxdPageParser.Parse(TwitterMetaPage);

        Assert.NotNull(rating);
        Assert.Equal(4.06, rating!.Value, precision: 2);
        Assert.Equal(0, rating.Count);
    }

    [Fact]
    public void Parse_NoAggregateRating_ReturnsNull()
    {
        Assert.Null(LetterboxdPageParser.Parse(NoRatingPage));
    }

    [Fact]
    public void Parse_EmptyOrGarbage_ReturnsNull()
    {
        Assert.Null(LetterboxdPageParser.Parse(string.Empty));
        Assert.Null(LetterboxdPageParser.Parse("<html><body>not a film page</body></html>"));
        Assert.Null(LetterboxdPageParser.Parse(
            "<script type=\"application/ld+json\">{ malformed json ]</script>"));
    }

    [Fact]
    public void Parse_OutOfRangeRating_ReturnsNull()
    {
        const string page = """
            <script type="application/ld+json">
            { "aggregateRating": { "ratingValue": 47, "ratingCount": 10 } }
            </script>
            """;
        Assert.Null(LetterboxdPageParser.Parse(page));
    }
}
