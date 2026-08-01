# Portfolio — Serverless C#/.NET

Personal portfolio site. Blazor WebAssembly frontend, Azure Functions API, hosted on
Azure Static Web Apps (Free tier), deployed by GitHub Actions.

## Hard constraints — do not violate

- **Everything targets `net8.0`.** SWA managed Functions do not support .NET 10, so the API
  cannot move there yet. Do not change any `<TargetFramework>`.
  - **Do not "upgrade" to .NET 9.** It *is* supported by SWA (`dotnet-isolated:9.0`), so it
    looks like a free win — it isn't. .NET 9 reaches end-of-support on **10 Nov 2026, the exact
    same day as .NET 8**, and it's STS rather than LTS. Zero extra runway, strictly worse.
  - **The planned move is .NET 8 → .NET 10** (LTS, supported to Nov 2028), as soon as SWA
    managed Functions support `dotnet-isolated:10.0`. Check the supported-values table at
    https://learn.microsoft.com/azure/static-web-apps/languages-runtimes — when `10.0` appears,
    the migration is: bump `<TargetFramework>` in **all four csproj files**, bump the
    framework-tied `Microsoft.AspNetCore.*` versions in `Directory.Packages.props`, change
    `apiRuntime` to `dotnet-isolated:10.0`, bump `global.json`, rebuild, test, deploy.
  - If Nov 2026 arrives with still no .NET 10 support, the app keeps running but stops getting
    security patches. The fallback is bring-your-own-functions, which lifts the runtime
    constraint but requires the Standard plan and loses API in PR preview environments.
- **`src/Portfolio.Api/Portfolio.Api.csproj` `<TargetFramework>` and the `apiRuntime` value in
  `staticwebapp.config.json` must always match** (`net8.0` ↔ `dotnet-isolated:8.0`).
  Changing one without the other breaks deployment.
- **`staticwebapp.config.json` lives at `src/Portfolio.Client/wwwroot/`**, never at the repo
  root. It must land in the *published* output to take effect. At the repo root it is silently
  ignored — the app still works locally and only fails on deep-link refresh in production.
- **The API is HTTP-triggered only.** The free tier's managed Functions support no other
  trigger type. No timer/cron, no queue, no Durable Functions. Anything stateful or scheduled
  would require the paid Standard plan.
- The SDK is pinned by `global.json`. Leave it pinned.

## Structure

```
Portfolio.sln
src/Portfolio.Client/    Blazor WebAssembly — the UI. Dev server: http://localhost:5257
  wwwroot/staticwebapp.config.json
src/Portfolio.Api/       Azure Functions, isolated worker. Dev server: http://localhost:7071
src/Portfolio.Shared/    DTOs shared by Client and Api. No dependencies on either.
tests/Portfolio.Tests/   xUnit. References Shared and Api.
```

Dependency direction: `Client → Shared`, `Api → Shared`, `Tests → Shared, Api`.
`Shared` must never reference `Client` or `Api`. Any type crossing the HTTP boundary belongs
in `Shared` so both sides compile against one definition.

### Layering — the rule that makes the tests useful

**Functions are adapters, not logic.** A function class does exactly three things: read input,
delegate to a service, map the result to a status code. Nothing else.

Business logic lives in plain classes that can be constructed with `new` in a test — no
`HttpRequest`, no Functions host, no Azure. This is not stylistic: logic written inside a
function class is effectively untestable, and `tests/Portfolio.Tests` becomes a project that
can't reach anything worth asserting on.

```
src/Portfolio.Api/
  Functions/      HTTP adapters. One class per endpoint. Thin.
  Services/       Business logic. Plain classes, no framework types.
  Validation/     Input rules.

src/Portfolio.Client/
  Pages/          Routable components.
  Layout/
  Components/     Reusable, non-routable.
  Services/       Typed API clients — the only place HttpClient is touched.

src/Portfolio.Shared/
  DTOs, and validation rules both sides must agree on. No I/O, no framework
  dependencies, nothing only one side needs.
```

**Razor components never call `HttpClient` directly.** Requests go through a typed service in
`Client/Services/`, so error handling, loading state, and deserialisation exist once rather
than being reinvented in every component.

### Design principles

- **No abstraction until there is a second concrete use for it.** The realistic failure mode
  for a project this size is over-engineering, not under-engineering. An interface with one
  implementation, a repository wrapping a single HTTP call, or a mediator for three endpoints
  all read as cargo-culting rather than judgement — especially in a repo written to be
  assessed. Add the seam when the second caller actually arrives.
- **Constructor injection only.** No static mutable state, no service locator, no passing
  `IServiceProvider` around. Static mutable state is also a correctness bug waiting to happen
  in a Functions host, where instances are reused across invocations.
- **Interfaces where they earn their place** — a boundary you genuinely test against or swap.
  Not by default, and not one per class.
- **Name things for what they do**, and keep a class to one reason to change. If a name needs
  "Helper", "Manager", or "Util", the responsibility probably isn't clear yet.

### Error handling

- **Expected failures are return values, not exceptions.** Invalid input and missing resources
  are normal control flow — return a result the caller maps to `400` or `404`. Exceptions for
  routine validation make the happy path harder to read and cost more than they're worth.
- **Exceptions are for genuinely unexpected conditions.** Let them bubble to the host and map
  to a generic `500`. Don't wrap every call in try/catch.
- **Never catch and swallow.** If you catch, either handle it meaningfully or log and rethrow.
  An empty catch block hides the failure you'll later need.
- What the caller sees on failure is covered under Security — never exception detail.

## Commands

```powershell
dotnet build Portfolio.sln          # build everything
dotnet test Portfolio.sln           # run the test suite
dotnet run --project src/Portfolio.Client   # frontend only
```

Full stack locally (frontend + API on one origin, like production) needs two terminals:

```powershell
# terminal 1
dotnet watch --project src/Portfolio.Client run
# terminal 2 — proxies the app and starts the Functions host, serving both on :4280
swa start
```

Then browse **http://localhost:4280**. Calling the API directly on :7071 bypasses the SWA
routing layer and will not reflect production behaviour.

## Security

**The single most important fact about this stack: Blazor WebAssembly runs in the browser.
Everything in `Portfolio.Client` is public.** The whole app — every DLL, every config file
under `wwwroot`, every string constant — is downloaded by the visitor and can be read with
browser devtools. There is no such thing as a hidden value in the client.

Consequences that are not negotiable:

- **No secrets in `Portfolio.Client`.** No API keys, connection strings, tokens, private
  endpoints, or email addresses you don't want scraped. Not in code, not in `wwwroot/appsettings.json`
  (which is served as a plain static file), not in a "temporarily" committed constant.
- **Authorization lives in the API, never in the UI.** Hiding a button or a component is a
  UX affordance, not a security control. Every rule enforced in the client must be enforced
  again server-side, because the client can be bypassed entirely.
- **`/api/*` is anonymous and publicly reachable.** `staticwebapp.config.json` sets
  `allowedRoles: ["anonymous"]` deliberately, so anyone can `curl` your endpoints directly
  without ever loading the site. Treat every request as hostile input from an unknown caller.

Input handling in the API:

- Validate everything server-side: required fields, maximum lengths, expected format. Reject
  with `400` early rather than working with half-valid data.
- **Cap input size.** Unbounded request bodies are both a denial-of-service risk and a cost
  risk — every invocation counts against the 1,000,000/month free-tier budget, and an abusive
  caller can burn it. Set explicit maximum lengths on every string field.
- Never interpolate user input into HTML. Blazor escapes by default, but **`MarkupString`
  bypasses that escaping entirely** — never pass user-supplied content to it. That is the one
  realistic XSS vector in a Blazor app.
- **Never return exception details, stack traces, or internal paths to the caller.** Log the
  detail server-side; return a generic message. Leaked stack traces disclose your structure
  and package versions.
- Don't log full request bodies or headers — they may carry personal data or credentials.

Contact submissions and personal data:

- **Submissions are delivered by structured logging to Application Insights**, not by email.
  `ContactService.Deliver` is the whole mechanism. This is deliberate: Azure Communication
  Services bills per message with no free allowance, so an anonymous public endpoint that
  sends one email per request is a billable amplifier with no rate limit in front of it.
  SendGrid withdrew its free tier in 2025. Logging has a real 5 GB/month grant and a hard
  daily cap, so abuse degrades service instead of generating an invoice.
- **Sampling is disabled in `host.json`.** Adaptive sampling drops traces, and traces *are*
  the delivery mechanism — a sampled-out trace is a lost message. Volume is controlled by
  `logLevels` and the portal daily cap instead. Don't re-enable it.
- **This intentionally stores personal data** — name, address, message body — for the
  resource's retention period. The privacy note on the form states this. If the retention
  period changes, change the note in the same commit.
- **If email delivery is ever added:** the recipient comes from configuration and *never*
  from the request, or the endpoint becomes an open relay for spamming arbitrary people from
  your domain. Send plain text only, never HTML, so hostile content cannot render in a mail
  client. `Reply-To` may carry the submitted address only because the validator rejects line
  breaks in it.

Known accepted risks:

- **There is no rate limiting.** Static Web Apps Free has none, and a serverless in-memory
  counter is useless across scaled-out instances. The honeypot plus cheap early validation is
  proportionate for a personal site, and the free-tier caps mean abuse costs availability
  rather than money. If it is ever actually abused, the escalation is Cloudflare Turnstile —
  which needs `script-src` and `connect-src` added to the CSP.
- **Client-rendered Blazor is weaker for SEO.** Content is rendered in the browser, so
  crawlers index it less reliably than server-rendered HTML. Accepted because demonstrating
  the stack is the point of this project. The fix, if discoverability ever matters more, is a
  Blazor Web App with server-side rendering — which needs a different hosting model than SWA
  managed functions.

Secrets handling:

- Locally: `src/Portfolio.Api/local.settings.json`, which is gitignored. Verify that before
  adding anything sensitive.
- In Azure: the Static Web App's application settings. These reach the API only, never the client.
- Never commit a secret "just to test". Rotate immediately if one is ever pushed — git history
  is public on this repo.

Dependencies:

- Keep the dependency list minimal and prefer first-party Microsoft packages. Every added
  NuGet package is code you ship and trust.
- Do not add Application Insights or OpenTelemetry packages until Application Insights is
  enabled on the Azure resource — see the deployment section for the ordering that matters.

## Code quality

**These rules are enforced by the build, not by memory.** `Directory.Build.props` sets
`TreatWarningsAsErrors` and enables the .NET analyzers at `latest-recommended` for every
project — a warning fails the build. `.editorconfig` carries style preferences and the two
deliberate analyzer suppressions (`CA1716`, `CA1848`), each with its reasoning inline. If a
new analyzer rule fires, fix the code; only suppress it in `.editorconfig` with a written
justification, never with a bare `#pragma`.

**Build configuration lives in three root files. Don't restate their settings in a `.csproj`** —
a local value silently overrides the central one, which is the drift these files exist to stop.

| File | Owns |
|---|---|
| `Directory.Build.props` | Nullable, implicit usings, analyzers, warnings-as-errors |
| `Directory.Packages.props` | Every NuGet version (`ManagePackageVersionsCentrally`). `PackageReference` carries no `Version` attribute |
| `.editorconfig` | Style, naming, analyzer severity overrides |

The generated Functions `WorkerExtensions` project is excluded from the strict settings by
name — it is machine-generated and not ours to fix.

**`<TargetFramework>` is the one property that must stay duplicated in every `.csproj`.**
Azure's Oryx builder detects the platform by text-scanning `.csproj` files for that element;
it does not evaluate MSBuild, so it cannot see the property inherited from
`Directory.Build.props`. Centralising it builds fine locally and then fails deployment with
`Could not detect any platform in the source directory`. Don't "tidy" it away.

- **Nullable reference types are on. Keep them on**, and fix nullability properly rather than
  silencing it with the `!` null-forgiving operator. A `!` is a claim you know better than the
  compiler; it should be rare and worth a comment.
- **`async` all the way down.** Return `async Task`, never block with `.Result` or `.Wait()` —
  those deadlock and starve the thread pool.
- **Propagate `CancellationToken`** through async call chains so abandoned requests stop doing work.
- **Never `new HttpClient()` per call.** Register it once in DI (`Program.cs`) and inject it;
  creating them per request exhausts sockets.
- Shared DTOs in `Portfolio.Shared` are immutable `record` types. Both sides compile against
  one definition, so a change that breaks the contract breaks the build rather than production.
- File-scoped namespaces (`namespace Portfolio.Api;`).
- One function class per endpoint.
- The client calls **relative** URLs (`/api/contact`) — never a hardcoded host. The origin
  differs between local, PR preview environments, and production.
- Prefer clear over clever. This is a portfolio: the code is read by people deciding whether
  to interview you.

## Definition of done

A change isn't finished until all of these hold. Check them before saying it's done.

- `dotnet build Portfolio.sln` — clean, **zero warnings** (warnings are errors anyway)
- `dotnet test Portfolio.sln` — passing, and new non-trivial logic came with tests
- `dotnet format Portfolio.sln --verify-no-changes` — clean, because CI runs it
- No new analyzer suppression without a written justification in `.editorconfig`
- No secret, key or personal data added to `Portfolio.Client`, to logs, or to git
- If a rule or constraint changed, this file was updated in the same commit
- Commit message says *why*, not just what

## Established patterns

Use these rather than inventing a parallel approach. Consistency is worth more here
than any individual improvement.

**Validation → `ValidationResult`.** `Portfolio.Shared.ValidationResult` is the one shape for
expected failures. Rules live in a static validator in `Shared` (e.g. `ContactValidator`) as a
pure function with no I/O, so the client and API run identical rules. The client's run is for
immediate feedback; **the API's run is authoritative**, because the client can be bypassed.

**Service → outcome enum + result record.** A service returns an outcome (`Accepted`,
`Invalid`, `Discarded`) plus any validation detail — see `ContactSubmissionResult`. The
function maps outcomes to status codes and does nothing else.

**Functions are adapters.** Read and bound the input, delegate, map the result. If a function
class needs a test, the logic is in the wrong place.

**Typed API clients.** Components inject a client from `Client/Services/` (e.g.
`ContactApiClient`), never `HttpClient`. The client owns deserialisation and turns every
failure mode into a result the component can render — components should never catch
`HttpRequestException`.

**Bound every input.** Cap request bodies before deserialising, and cap every string field.
`ContactLimits` holds the numbers so the form's `maxlength` and the server's validation cannot
drift apart.

**Guard public entry points.** `ArgumentNullException.ThrowIfNull` on public service methods.
A null there is a programming error, not user input — that distinction is why it throws while
invalid input returns a result.

**Constructor injection only.** No static mutable state — Functions reuses instances across
invocations, so static state is a correctness bug, not just a style preference.

## Design system

The visual language is Microsoft's, chosen to signal the tech stack. All tokens are defined
once in `src/Portfolio.Client/wwwroot/css/app.css` under `:root`.

**Never hardcode a colour.** Use the variables — a literal hex outside the `:root` block is a
bug, because it won't follow dark mode and won't stay consistent.

| Token | Use |
|---|---|
| `--primary` (`#0078D4`, Fluent communication blue) | Anything interactive: links, buttons, focus, active nav |
| `--ms-red` `--ms-green` `--ms-blue` `--ms-yellow` | Accents only — section markers, stack pills, the four-colour rule |
| `--grey-*` ramp / `--text`, `--text-muted`, `--border`, `--bg`, `--bg-subtle`, `--surface` | Everything else |

**The four logo colours are structural, not decorative.** They mark distinct things — one per
section, one per stack pill, the four-part rule between regions. Don't use them for emphasis
inside body text, and don't introduce a fifth accent colour: the set is the point.

Layout and CSS rules:

- **No CSS framework.** Bootstrap was removed deliberately — it supplied a generic look that
  reads as "template", and cost ~230 KB on every first load against a bandwidth-metered free
  tier. Don't reintroduce it or add Tailwind; write the CSS.
- **No webfonts.** The CSP sets `font-src 'self'`, so Google Fonts and similar are blocked by
  design. The stack leads with Segoe UI, which is correct for this theme anyway.
- **Component styles go in scoped `.razor.css` files**, next to the component. Only genuinely
  shared building blocks (`.container`, `.btn`, `.tech-pill`, `.ms-squares`, `.ms-rule`,
  tokens, resets) belong in `app.css`.
- Layout components live in `Layout/` (`MainLayout`, `NavMenu`, `Footer`). Page-specific
  styling lives beside the page, e.g. `Pages/Home.razor.css`.
- **Breakpoints in `rem`, not `px`**, so they respond to the user's font size. Existing ones:
  `40rem` (nav collapses), `48rem` (footer stacks).
- Prefer `clamp()` for type and section spacing over fixed sizes at each breakpoint.

Accessibility baseline — these are already in place, don't regress them:

- Visible `:focus-visible` outline on every interactive element. Never remove an outline
  without providing an equivalent.
- The skip-to-content link in `MainLayout` must stay first in the DOM.
- Dark mode via `prefers-color-scheme`. Any new colour must work in both themes, which is
  automatic if you use the tokens.
- `prefers-reduced-motion` disables smooth scrolling and transitions.
- Decorative elements (the square marks) carry `aria-hidden="true"`; interactive controls carry
  a label and, where they toggle, `aria-expanded` / `aria-controls`.

## Testing

- Add a test in `tests/Portfolio.Tests` for any non-trivial logic — validation, parsing,
  formatting, anything with a branch worth being sure about.
- Validation rules especially: they're the security boundary, they're pure functions, and
  they're cheap to test.
- Don't write tests that just assert framework behaviour or restate the implementation.
- `dotnet test Portfolio.sln` must pass before any commit.

CI runs `.github/workflows/ci.yml` (build + test in Release, plus an advisory vulnerable-package
scan) on every push and pull request. Note that the deploy workflow does **not** run tests —
it builds and ships. CI is the only thing standing between a broken test and production.

## Deployment

**Live at https://ambitious-coast-0312a8e0f.7.azurestaticapps.net** (East US 2, Free SKU).

Push to `main` → `.github/workflows/azure-static-web-apps-ambitious-coast-0312a8e0f.yml` →
Azure Static Web Apps. Path settings, which must not change:

```yaml
app_location:    "src/Portfolio.Client"
api_location:    "src/Portfolio.Api"
output_location: "wwwroot"
```

Deployment constraints learned the hard way — do not undo these:

- **Application Insights is enabled** on the Static Web App, so
  `APPLICATIONINSIGHTS_CONNECTION_STRING` is present as a linked application setting and the
  Functions host ships request telemetry and forwards `ILogger` output automatically.
  Monitoring is configured through `logging.applicationInsights` in `src/Portfolio.Api/host.json`.
  - **Do not add the worker-level OpenTelemetry packages back.** `func` templates generate
    `UseAzureMonitorExporter()` plus `"telemetryMode": "OpenTelemetry"`. Host-level
    integration already covers what this project needs, and that wiring is what broke the
    first deployment — when the connection string was absent the worker threw on startup and
    the deploy failed with only a generic "Failed to deploy the Azure Functions". If you
    regenerate the Functions project, strip it again.
  - Application Insights bills separately from Static Web Apps and has a 5 GB/month free
    ingestion grant. A **daily ingestion cap** is set on the resource so it stops ingesting
    rather than billing. Keep that cap in place.
- **`global.json` must hold a version *floor*, not an exact pin.** Oryx ships its own SDK patch
  (8.0.420 at time of writing) and `rollForward` only rolls up. An exact pin fails CI while
  building fine locally.
- **Keep workflow paths forward-slashed.** The runner is Linux; a Windows backslash in
  `api_location` silently fails to resolve.

## Free-tier budget ($0)

100 GB bandwidth/month · 250 MB storage per environment · 1,000,000 function executions/month ·
2 custom domains · 3 staging environments. Bandwidth has no overage billing — serving pauses
instead. Keep assets small; prefer compressed images over large uncompressed ones.
