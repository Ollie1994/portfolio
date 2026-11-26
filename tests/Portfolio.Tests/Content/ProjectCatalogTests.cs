using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Portfolio.Client;
using Portfolio.Client.Content;

namespace Portfolio.Tests.Content;

/// <summary>
/// ProjectCatalog is the one list of projects, but three things it cannot generate
/// still have to agree with it: each page's @page route (a route must be a constant),
/// and the static sitemap.xml. These tests are what keeps them in step.
/// </summary>
public class ProjectCatalogTests
{
    private static readonly string[] LogoAccents = ["accent-red", "accent-green", "accent-blue", "accent-yellow"];

    private static IEnumerable<Project> WithPages => ProjectCatalog.All.Where(p => p.Detail is not null);

    public static TheoryData<string> PageHrefs()
    {
        var data = new TheoryData<string>();
        foreach (var project in WithPages)
        {
            data.Add(project.Href!);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PageHrefs))]
    public void EveryProjectWithAPage_HasAComponentRoutedAtItsAddress(string href)
    {
        var routed = typeof(App).Assembly.GetTypes()
            .Where(t => t.GetCustomAttributes<RouteAttribute>().Any(r => r.Template == href))
            .ToList();

        Assert.Single(routed);
    }

    [Fact]
    public void Slugs_AreUnique()
    {
        var slugs = WithPages.Select(p => p.Detail!.Slug).ToList();

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
        var sitemap = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Portfolio.Client", "wwwroot", "sitemap.xml"));
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var listed = sitemap.Descendants(ns + "loc")
            .Select(loc => new Uri(loc.Value).AbsolutePath)
            .Order(StringComparer.Ordinal)
            .ToList();

        var expected = WithPages.Select(p => p.Href!)
            .Append("/")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, listed);
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Portfolio.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Portfolio.sln not found above the test output directory.");
    }
}
