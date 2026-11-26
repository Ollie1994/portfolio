using System.Text.RegularExpressions;
using System.Xml.Linq;
using Portfolio.Client.Content;

namespace Portfolio.Tests.Content;

/// <summary>
/// The production address is written out by hand in three static files that code cannot
/// generate. These keep them on <see cref="Site.Origin"/>, so moving to a custom domain
/// (#35) fails here until every copy has moved.
/// </summary>
public class SiteTests
{
    private static string Published(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));

    [Fact]
    public void Origin_IsAnHttpsAddressWithNoPathOrTrailingSlash()
    {
        var origin = new Uri(Site.Origin);

        Assert.Equal(Uri.UriSchemeHttps, origin.Scheme);
        Assert.Equal(Site.Origin, origin.GetLeftPart(UriPartial.Authority));
    }

    [Theory]
    [InlineData("og:url")]
    [InlineData("og:image")]
    public void SocialTags_PointAtTheOrigin(string property)
    {
        var tag = Regex.Match(Published("index.html"), $"<meta property=\"{Regex.Escape(property)}\" content=\"(?<url>[^\"]+)\"");

        Assert.True(tag.Success, $"index.html has no {property}.");
        Assert.StartsWith(Site.Origin + "/", tag.Groups["url"].Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Sitemap_IsOnTheOrigin()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = XDocument.Parse(Published("sitemap.xml")).Descendants(ns + "loc").Select(l => l.Value).ToList();

        Assert.NotEmpty(locs);
        Assert.All(locs, loc => Assert.StartsWith(Site.Origin + "/", loc, StringComparison.Ordinal));
    }

    [Fact]
    public void Robots_PointsAtTheSitemapOnTheOrigin()
    {
        Assert.Contains($"Sitemap: {Site.Origin}/sitemap.xml", Published("robots.txt"), StringComparison.Ordinal);
    }
}
