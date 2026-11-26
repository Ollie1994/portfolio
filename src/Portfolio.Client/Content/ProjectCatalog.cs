namespace Portfolio.Client.Content;

/// <summary>A link shown under a project page's header.</summary>
public sealed record ProjectLink(string Label, string Href);

/// <summary>
/// What a project's own page shows above its body. A project without one is a card
/// with no link.
/// </summary>
/// <param name="Slug">The page is at /projects/{Slug}. Its <c>@page</c> directive has to
/// repeat it, since a route must be a constant; ProjectCatalogTests checks they agree.</param>
public sealed record ProjectDetail(
    string Slug,
    string Summary,
    IReadOnlyList<string> Tech,
    IReadOnlyList<ProjectLink> Links)
{
    public string Href => $"/projects/{Slug}";
}

/// <summary>Everything about a project except the body text of its page.</summary>
public sealed record Project(
    string Title,
    string Kicker,
    string AccentClass,
    string? CardSummary,
    IReadOnlyList<string> CardTech,
    ProjectDetail? Detail)
{
    public string? Href => Detail?.Href;
}

/// <summary>
/// The one list of projects. The home page's cards and each project page's header
/// both read it, so a project's title, status or accent changes in one place. A
/// project page finds its entry by its own address (<see cref="ForPath"/>), so a
/// page cannot show another project's header. Five static copies are kept in step by
/// hand: the page's route in staticwebapp.config.json and the sitemap (both checked by
/// tests), the &lt;noscript&gt; fallback in index.html, the social card in tools/og-image,
/// and the deep link /projects/vam that the deploy workflow checks after each deploy.
/// After changing the card, render it and bump ?v= on og:image in index.html, or shared
/// links keep the old one.
/// </summary>
public static class ProjectCatalog
{
    public static Project Vam { get; } = new(
        Title: "Väm",
        Kicker: "Soft launch · 2026",
        AccentClass: "accent-red",
        CardSummary: "Built a map of where Gothenburg's food trucks are today. I enter and check every location myself, after dropping an automatic version, because a wrong pin costs more than a missing one.",
        CardTech: ["TypeScript", "Next.js", "Postgres", "MapLibre"],
        Detail: new(
            Slug: "vam",
            Summary: "Built a phone-friendly map of where Gothenburg's food trucks are today. Every location is entered by hand and checked before it appears, instead of the automatic version first planned. Visitors are served a shared copy of the map, refreshed about once a minute, so it stays fast and cheap however many people use it.",
            Tech: ["TypeScript", "Next.js 16", "React 19", "Supabase Postgres", "MapLibre GL JS", "Protomaps", "Tailwind CSS v4", "Vitest", "Vercel"],
            Links:
            [
                new("Visit the site", "https://vararmaten.vercel.app"),
                new("Source on GitHub", "https://github.com/Ollie1994/whereisfood")
            ]));

    public static Project Seeding { get; } = new(
        Title: "Database seeding system",
        Kicker: "Professional work · 2025–26",
        AccentClass: "accent-green",
        CardSummary: "Built a tool at Klarr, a development agency, that gives an education platform's developers realistic test data they can recreate at any time, for daily work and automated tests. My thesis subject.",
        CardTech: ["JavaScript", "Express", "MongoDB", "AJV"],
        Detail: new(
            Slug: "seeding",
            Summary: "Built a tool at Klarr that gives an education platform's developers realistic test data on demand, in whatever amount a task needs. They can recreate it at any time, and use it in daily work and in the automated tests.",
            Tech: ["JavaScript", "Express.js", "MongoDB", "Mongoose", "Faker", "AJV", "Lodash"],
            Links: []));

    public static Project Portfolio { get; } = new(
        Title: "This portfolio site",
        Kicker: "Live · 2026",
        AccentClass: "accent-blue",
        CardSummary: "Built and run this site for $0 a month, on free plans that pause rather than bill if overused. The contact form is built to withstand misuse, and quality checks run on every change.",
        CardTech: ["C#", ".NET 8", "Blazor", "Azure"],
        Detail: new(
            Slug: "portfolio",
            Summary: "Built and run this site and its contact form for $0 a month, on Microsoft's free cloud plans, chosen because heavy use pauses them rather than sending a bill. The site runs in your browser, the form is built to withstand misuse, and every change to the code is checked automatically.",
            Tech: ["C#", ".NET 8", "Blazor WebAssembly", "Azure Functions", "Azure Static Web Apps", "GitHub Actions", "xUnit", "Application Insights"],
            Links: [new("Source on GitHub", "https://github.com/Ollie1994/portfolio")]));

    public static Project Quiz { get; } = new(
        Title: "Real-time quiz app",
        Kicker: "Learning project · 2025",
        AccentClass: "accent-yellow",
        CardSummary: "Built a multiplayer quiz where a host runs the game and players answer on their phones. Ran several games at once on a rented Amazon server as a demo, learning by doing how apps with instant updates work and how to put them online.",
        CardTech: ["JavaScript", "Socket.IO", "Next.js", "AWS"],
        Detail: new(
            Slug: "quiz",
            Summary: "Built a Kahoot-style multiplayer quiz: a host runs the game on a big screen while players join from their phones with a room code. Ran several games at once on a rented Amazon server as a demo, and learned by doing how apps with instant updates work and how to put them online.",
            Tech: ["JavaScript", "Next.js", "React", "Socket.IO", "MongoDB Atlas", "Prisma", "AWS EC2", "PM2"],
            Links: [new("Source on GitHub", "https://github.com/Ollie1994/kahoot_clone_v5")]));

    /// <summary>Nothing to show yet: a card with no link, summary or page.</summary>
    public static Project ApplicationTracker { get; } = new(
        Title: "ApplicationTracker",
        Kicker: "In the works",
        AccentClass: "accent-neutral",
        CardSummary: null,
        CardTech: ["Java", "Spring Boot", "Microservices", "Spring Cloud"],
        Detail: null);

    /// <summary>
    /// In home page order, which is deliberate (see CLAUDE.md): Väm first as the most
    /// current, the quiz last of the finished ones, then the unfinished one.
    /// </summary>
    public static IReadOnlyList<Project> All { get; } = [Vam, Seeding, Portfolio, Quiz, ApplicationTracker];

    /// <summary>
    /// The project whose page is at <paramref name="path"/>, or null if none is.
    /// Case-insensitive and ignoring a trailing slash, as Blazor's routing is.
    /// </summary>
    public static Project? ForPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var trimmed = path.TrimEnd('/');
        return All.FirstOrDefault(p => string.Equals(p.Href, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
