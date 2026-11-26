using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Portfolio.Client.Content;

namespace Portfolio.Tests.Content;

/// <summary>
/// The production address and the home page's description are written out by hand in
/// files that code cannot generate. These keep them on <see cref="Site"/>, so moving to a
/// custom domain (#35) fails here until every checked copy has moved.
/// </summary>
public class SiteTests
{
    private static string Published(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file));

    private static string? MetaProperty(string property)
    {
        var tag = Regex.Match(Published("index.html"), $"<meta property=\"{Regex.Escape(property)}\" content=\"(?<value>[^\"]+)\"");
        return tag.Success ? WebUtility.HtmlDecode(tag.Groups["value"].Value) : null;
    }

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
        var url = MetaProperty(property);

        Assert.True(url is not null, $"index.html has no {property}.");
        Assert.StartsWith(Site.Origin + "/", url, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkPreviewDescription_IsTheHomePageDescription()
    {
        // Two copies: link previews read og:description from index.html without running
        // the app, and search engines read the one the home page sets from Site.
        Assert.Equal(Site.Description, MetaProperty("og:description"));
    }

    [Fact]
    public void Readme_LinksToTheOrigin()
    {
        Assert.Contains($"**Live:** {Site.Origin}", Published("README.md"), StringComparison.Ordinal);
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
