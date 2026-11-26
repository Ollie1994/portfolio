namespace Portfolio.Client.Content;

/// <summary>Facts about the site as a whole.</summary>
public static class Site
{
    /// <summary>
    /// The production address, for canonical links. Always this one, also on a preview
    /// or locally: a canonical link says which copy of a page search engines should
    /// index, and that is the live one.
    ///
    /// Repeated as static text in index.html (og:url, og:image), sitemap.xml, robots.txt
    /// and the README's "Live" link. SiteTests checks those four agree with this, so for a
    /// new domain (#35) the failing tests list them. Not checked: the address in
    /// CLAUDE.md, and the preview addresses in the workflows, which keep the Azure name.
    /// </summary>
    public const string Origin = "https://ambitious-coast-0312a8e0f.7.azurestaticapps.net";

    /// <summary>
    /// The home page's search result description. index.html repeats it as og:description
    /// for link previews, which cannot run the app; SiteTests checks the two match.
    /// </summary>
    public const string Description =
        "Each project written up with the reasoning behind it — what I built and why it's built that way. C#, JavaScript, Java and TypeScript.";
}
