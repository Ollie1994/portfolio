namespace Portfolio.Client.Content;

/// <summary>Facts about the site as a whole.</summary>
public static class Site
{
    /// <summary>
    /// The production address, for canonical links. Always this one, also on a preview
    /// or locally: a canonical link says which copy of a page search engines should
    /// index, and that is the live one. index.html, sitemap.xml and robots.txt repeat it
    /// as static text; SiteTests checks they agree, so a new domain (#35) is one change
    /// here and the test lists the rest.
    /// </summary>
    public const string Origin = "https://ambitious-coast-0312a8e0f.7.azurestaticapps.net";
}
