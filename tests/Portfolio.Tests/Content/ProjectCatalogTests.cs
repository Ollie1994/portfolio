using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Portfolio.Client;
using Portfolio.Client.Content;

namespace Portfolio.Tests.Content;

/// <summary>
/// ProjectCatalog is the one list of projects, and a project page finds its entry by
/// its own address. Two things the catalogue cannot generate still have to agree with
/// it, and these tests keep them in step: each page's @page route (a route must be a
/// constant), checked in both directions, and the static sitemap.xml. The &lt;noscript&gt;
/// links in index.html are kept in step by hand and not tested.
/// </summary>
public class ProjectCatalogTests
{
    private static readonly string[] LogoAccents = ["accent-red", "accent-green", "accent-blue", "accent-yellow"];

    /// <summary>Routes under /projects/ that redirect rather than show a project.</summary>
    private static readonly string[] Redirects = ["/projects/whereisfood"];

    private static IEnumerable<ProjectDetail> Details => ProjectCatalog.All.Select(p => p.Detail).OfType<ProjectDetail>();

    private static IEnumerable<string> RouteTemplates => typeof(App).Assembly.GetTypes()
        .SelectMany(t => t.GetCustomAttributes<RouteAttribute>())
        .Select(r => r.Template);

    public static TheoryData<string> PageHrefs()
    {
        var data = new TheoryData<string>();
        foreach (var detail in Details)
        {
            data.Add(detail.Href);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PageHrefs))]
    public void EveryProjectWithAPage_HasExactlyOneComponentRoutedAtItsAddress(string href)
    {
        Assert.Single(RouteTemplates, template => template == href);
    }

    [Fact]
    public void EveryRouteUnderProjects_IsAProjectWithAPage_OrAKnownRedirect()
    {
        // The other direction: a new page with no catalogue entry would have no card,
        // no sitemap entry, and would throw when ProjectPage looks itself up.
        var unaccounted = RouteTemplates
            .Where(t => t.StartsWith("/projects/", StringComparison.Ordinal))
            .Where(t => !Redirects.Contains(t))
            .Where(t => ProjectCatalog.ForPath(t)?.Detail is null)
            .ToList();

        Assert.Empty(unaccounted);
    }

    [Fact]
    public void KnownRedirects_StillExist()
    {
        // Otherwise the exemption above could outlive the page it was for.
        Assert.All(Redirects, redirect => Assert.Contains(redirect, RouteTemplates));
    }

    [Theory]
    [InlineData("/projects/vam")]
    [InlineData("/projects/VAM")]
    [InlineData("/projects/vam/")]
    public void ForPath_FindsAProjectAsBlazorsRoutingWould(string path)
    {
        Assert.Same(ProjectCatalog.Vam, ProjectCatalog.ForPath(path));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/projects/")]
    [InlineData("/projects/nope")]
    [InlineData("/projects/vam/extra")]
    public void ForPath_FindsNothingWhereThereIsNoProjectPage(string path)
    {
        Assert.Null(ProjectCatalog.ForPath(path));
    }

    [Fact]
    public void Slugs_AreUnique()
    {
        var slugs = Details.Select(d => d.Slug).ToList();

        Assert.Equal(slugs.Count, slugs.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Accents_FollowTheRule()
    {
        // One of the four logo colours for a project with a page; neutral grey, and
        // only grey, for one with nothing to show yet. See CLAUDE.md.
        Assert.All(ProjectCatalog.All, p =>
        {
            if (p.Detail is null)
            {
                Assert.Equal("accent-neutral", p.AccentClass);
            }
            else
            {
                Assert.Contains(p.AccentClass, LogoAccents);
            }
        });
    }

    [Fact]
    public void Sitemap_ListsTheHomePageAndEveryProjectPage_AndNothingElse()
    {
        // Linked into the test output from src/Portfolio.Client/wwwroot by the csproj.
        var sitemap = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "sitemap.xml"));
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var listed = sitemap.Descendants(ns + "loc")
            .Select(loc => new Uri(loc.Value).AbsolutePath)
            .Order(StringComparer.Ordinal)
            .ToList();

        var expected = Details.Select(d => d.Href)
            .Append("/")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, listed);
    }
}
