# Portfolio

[![CI](https://github.com/Ollie1994/portfolio/actions/workflows/ci.yml/badge.svg)](https://github.com/Ollie1994/portfolio/actions/workflows/ci.yml)

Personal portfolio site. **Blazor WebAssembly** frontend, **Azure Functions** API, hosted on
Azure Static Web Apps and deployed by GitHub Actions. Runs at **$0**.

**Live:** https://ambitious-coast-0312a8e0f.7.azurestaticapps.net

## Architecture

```
Browser ──► Azure Static Web Apps (global CDN)
              ├── /*      Blazor WebAssembly client — C# compiled to WebAssembly
              └── /api/*  Azure Functions, .NET 8 isolated worker
```

Both are served from a single Static Web Apps resource, so the API is same-origin: no CORS
configuration and no API host to switch between local, preview and production.

| Project | Purpose |
|---|---|
| `src/Portfolio.Client` | Blazor WebAssembly UI |
| `src/Portfolio.Api` | HTTP-triggered Azure Functions (isolated worker) |
| `src/Portfolio.Shared` | DTOs and validation shared by both sides |
| `tests/Portfolio.Tests` | xUnit suite |

Dependency direction is `Client → Shared`, `Api → Shared`, `Tests → Shared, Api`. Anything
crossing the HTTP boundary lives in `Shared`, so a change that breaks the contract breaks the
build rather than production.

**Function classes are adapters, not logic.** A function reads input, delegates to a plain
service, and maps the result to a status code. Business logic lives in ordinary classes that
construct with `new` in a test — no HTTP types, no Functions host, no Azure.

## Running locally

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0),
[Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local),
and the [SWA CLI](https://azure.github.io/static-web-apps-cli/).

```bash
dotnet build Portfolio.sln
dotnet test Portfolio.sln
dotnet format Portfolio.sln --verify-no-changes   # CI enforces this
```

For the full stack on one origin, as it behaves in production, use two terminals:

```bash
# terminal 1
dotnet watch --project src/Portfolio.Client run

# terminal 2
swa start
```

Then open <http://localhost:4280>. Calling the Functions host directly on `:7071` bypasses the
Static Web Apps routing layer and won't reflect production.

The Functions host needs a storage emulator: `local.settings.json` sets `AzureWebJobsStorage`
to `UseDevelopmentStorage=true`, so install and start
[Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
(`npm install -g azurite`, then `azurite`) before running the API.

## The contact endpoint

`/api/*` is anonymous and directly reachable, and everything in a WebAssembly client is public,
so the endpoint treats every request as hostile input:

- **Validation rules live in `Shared` and run on both sides.** The client's run gives immediate
  feedback; the API's is authoritative, because the client can be bypassed entirely.
- **Request bodies are bounded twice** — on the declared `Content-Length`, then again through a
  limited stream during deserialisation, since the declared length comes from the caller.
- **`Content-Type: application/json` is required.** A cross-site HTML form can post form-encoded
  or plain text without consent, but cannot set JSON without a preflight.
- **Line breaks are rejected in single-line fields**, removing email header injection as a class
  of bug regardless of how delivery is implemented.
- **A honeypot field** catches naive bots; their submissions report success, because telling a
  bot it was detected only helps it adapt.

**Submissions are delivered to Application Insights as structured logs, not by email.** Azure
Communication Services bills per message with no free allowance, which would make an anonymous
public endpoint a billable amplifier, and SendGrid withdrew its free tier in 2025. Application
Insights has a 5 GB/month free grant and a hard daily ingestion cap, so abuse costs availability
rather than money. The email address is published on the site as well, so the form is a
convenience rather than the only channel.

## Constraints

**Everything targets `net8.0`.** Static Web Apps managed functions don't support a
`dotnet-isolated:10.0` runtime, and .NET 8 is the newest LTS they do. .NET 9 was considered and
rejected: it reaches end of support on the same day as .NET 8 while being STS rather than LTS,
so it buys no runway. The `<TargetFramework>` in the API and the `apiRuntime` in
`staticwebapp.config.json` must always match.

**Managed functions rather than bring-your-own.** Managed functions keep the site on the free
tier with one deployment pipeline and working PR preview environments. Bringing your own
Function App allows any runtime and non-HTTP triggers, but requires the paid Standard plan and
loses API support in preview environments.

## Code quality

Enforced by the build rather than by convention:

- `Directory.Build.props` sets `TreatWarningsAsErrors` and runs the .NET analyzers at
  `latest-recommended`
- `Directory.Packages.props` holds every NuGet version centrally; `PackageReference` entries
  carry no `Version`
- `.editorconfig` holds style, naming and two documented analyzer suppressions

`main` requires a pull request with `Build and Test` passing. CI runs build, tests, a formatting
check and a vulnerable-package scan. Every pull request gets its own preview environment.

## Security headers

`staticwebapp.config.json` sets a Content Security Policy with no `unsafe-inline`, plus
`X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy`, `X-Frame-Options` and HSTS.

## Cost

Runs at **$0** on the Static Web Apps free tier: 100 GB bandwidth/month, 250 MB storage per
environment, 1,000,000 function executions/month. Bandwidth has no overage billing — serving
pauses rather than charging. Application Insights bills separately but has a 5 GB/month free
grant with a daily ingestion cap set, so it stops collecting rather than invoicing.
