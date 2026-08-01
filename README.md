# Portfolio

A serverless personal portfolio site built with **Blazor WebAssembly** and **Azure Functions**,
hosted on Azure Static Web Apps and deployed by GitHub Actions.

**Live:** https://ambitious-coast-0312a8e0f.7.azurestaticapps.net

> Status: infrastructure complete, content in progress. The pipeline, hosting, API and test
> setup are working end to end; the site itself is still the starting scaffold.

## Architecture

```
Browser ──► Azure Static Web Apps (global CDN)
              ├── /*      Blazor WebAssembly client (static files)
              └── /api/*  Azure Functions, .NET 8 isolated worker
```

The frontend compiles to WebAssembly and runs entirely in the browser. The API runs as
Static Web Apps *managed functions* — Azure provisions and scales the Functions host as part
of the same resource, so there is no separate Function App to deploy or pay for. Both are
served from the same origin, which means no CORS configuration and no hardcoded API host.

| Project | Purpose |
|---|---|
| `src/Portfolio.Client` | Blazor WebAssembly UI |
| `src/Portfolio.Api` | HTTP-triggered Azure Functions (isolated worker) |
| `src/Portfolio.Shared` | DTOs and validation shared by both sides |
| `tests/Portfolio.Tests` | xUnit test suite |

Business logic lives in plain, testable classes; function classes are thin HTTP adapters.

## Running locally

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0),
[Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local),
and the [SWA CLI](https://azure.github.io/static-web-apps-cli/).

```bash
dotnet build Portfolio.sln
dotnet test Portfolio.sln
```

To run the full stack the way it behaves in production — frontend and API on one origin —
use two terminals:

```bash
# terminal 1
dotnet watch --project src/Portfolio.Client run

# terminal 2
swa start
```

Then open <http://localhost:4280>. Hitting the Functions host directly on `:7071` bypasses the
Static Web Apps routing layer and won't reflect production behaviour.

## Deployment

Every push to `main` triggers two workflows:

- **CI** — builds and tests in Release, and scans for vulnerable packages
- **Azure Static Web Apps CI/CD** — builds and deploys the site and API

Pull requests get their own staging environment automatically.

## Notable technical decisions

**.NET 8 rather than .NET 10.** Static Web Apps managed functions don't support a
`dotnet-isolated:10.0` runtime yet, and .NET 8 is the newest LTS they do support. .NET 9 was
considered and rejected: it reaches end-of-support on the same day as .NET 8 while being STS
rather than LTS, so it offers no additional runway.

**Managed functions rather than bring-your-own.** Managed functions keep the whole site on the
free tier with a single deployment pipeline and working PR preview environments. Bringing your
own Function App would allow any runtime version and non-HTTP triggers, but requires the paid
Standard plan and loses API support in preview environments — neither of which this workload
needs.

**Warnings as errors.** `Directory.Build.props` enables the .NET analyzers at
`latest-recommended` and treats warnings as errors across every project. The two suppressions
in `.editorconfig` are documented with reasons.

**Security headers at the edge.** `staticwebapp.config.json` sets a Content Security Policy,
`X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` and HSTS globally.

See [CLAUDE.md](CLAUDE.md) for the full constraint set, conventions, and the deployment
gotchas discovered while setting this up.

## Cost

Runs at **$0** on the Static Web Apps free tier: 100 GB bandwidth/month, 250 MB storage per
environment, and 1,000,000 function executions/month. Bandwidth has no overage billing —
serving pauses rather than charging.
