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
    the migration is: bump `<TargetFramework>` in all four csproj files, change `apiRuntime` to
    `dotnet-isolated:10.0`, bump `global.json`, rebuild, test, deploy.
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

Secrets handling:

- Locally: `src/Portfolio.Api/local.settings.json`, which is gitignored. Verify that before
  adding anything sensitive.
- In Azure: the Static Web App's application settings. These reach the API only, never the client.
- Never commit a secret "just to test". Rotate immediately if one is ever pushed — git history
  is public on this repo.

Dependencies:

- Keep the dependency list minimal and prefer first-party Microsoft packages. Every added
  NuGet package is code you ship and trust.
- Do not add Application Insights or OpenTelemetry — see the deployment section for why it
  breaks this specific host.

## Code quality

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

## Testing

- Add a test in `tests/Portfolio.Tests` for any non-trivial logic — validation, parsing,
  formatting, anything with a branch worth being sure about.
- Validation rules especially: they're the security boundary, they're pure functions, and
  they're cheap to test.
- Don't write tests that just assert framework behaviour or restate the implementation.
- `dotnet test Portfolio.sln` must pass before any commit.

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

- **Never add Application Insights / OpenTelemetry to the API.** `func` templates generate
  `UseAzureMonitorExporter()` plus `"telemetryMode": "OpenTelemetry"`, which require
  `APPLICATIONINSIGHTS_CONNECTION_STRING`. SWA managed functions never provide it, so the
  worker throws on startup and the deploy fails with only a generic "Failed to deploy the
  Azure Functions". If you regenerate the Functions project, strip that wiring again.
- **`global.json` must hold a version *floor*, not an exact pin.** Oryx ships its own SDK patch
  (8.0.420 at time of writing) and `rollForward` only rolls up. An exact pin fails CI while
  building fine locally.
- **Keep workflow paths forward-slashed.** The runner is Linux; a Windows backslash in
  `api_location` silently fails to resolve.

## Free-tier budget ($0)

100 GB bandwidth/month · 250 MB storage per environment · 1,000,000 function executions/month ·
2 custom domains · 3 staging environments. Bandwidth has no overage billing — serving pauses
instead. Keep assets small; prefer compressed images over large uncompressed ones.
