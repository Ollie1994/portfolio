# Serverless C#/.NET Portfolio — Setup Checklist

**Stack:** Blazor WebAssembly (C#) · Azure Static Web Apps (Free tier) · Azure Functions (C#, isolated) · GitHub Actions CI/CD · VS Code

**Target framework:** .NET 8 (LTS) — required for Static Web Apps managed-Functions compatibility (do NOT use .NET 10 for the API; SWA does not support it yet).

---

## 1. Accounts

- [x] **Microsoft / Azure account** — with an active Azure subscription (free one works; payment card required on file, but the Static Web App itself won't bill) — created and signed in via the VS Code Azure extension
- [x] **GitHub account** — hosts the repo and runs CI/CD — signed in as `Ollie1994` via `gh`
- [x] **Anthropic account** — for signing into Claude Code (Claude Pro/Max plan or API credits)

## 2. Software to download & install

- [x] **VS Code** — the editor
- [x] **.NET 8 SDK (LTS)** — the framework/build tooling; target .NET 8 to stay SWA-compatible — `8.0.423`
- [x] **Node.js (LTS)** — prerequisite for the Claude Code CLI and SWA CLI (no JS coding needed) — `v26.13.0` (set up on `v22.14.0`). **`swa start` needs Node 18–22:** the SWA CLI refuses a newer Node when it starts the Functions host, and 2.1.0 still does (checked in its source), so updating it does not help. Keep a Node 22 alongside, e.g. with nvm-windows, for full-stack local runs
- [x] **Git** — version control + GitHub connection
- [x] **Azure Functions Core Tools** — local runtime to run/debug C# Functions — `4.12.1` (4.17.0 available)
- [x] **Azure Static Web Apps CLI (SWA CLI)** — emulates the real Azure environment locally (frontend + API on one address) — `2.0.10` (2.1.0 available)
- [x] **Claude Code CLI** — agentic coding tool
- [x] **Modern browser (Chrome/Edge)** — for running/debugging the Blazor WASM app — both installed

## 3. VS Code extensions

- [x] **C# Dev Kit** (Microsoft) — core C#/.NET experience — `v3.40.215`
- [x] **C#** extension — installed as a dependency of C# Dev Kit — `v2.160.4`
- [x] **Azure Static Web Apps** extension — create/deploy tooling — `v0.13.4`
- [x] **Azure Functions** extension — local run/debug + deploy for the API — `v1.23.0`
- [x] **Azure Resources / Azure Account** extension — sign-in and resource management — `v0.13.3`

The repository's `.vscode/extensions.json` recommends C# Dev Kit and the Static Web Apps and
Functions extensions, so VS Code offers them when the folder is opened.
- [x] **Claude Code** extension — native integration (panel, inline diffs, auto context sharing)

## 4. Things to create

- [x] **GitHub repository** — for the portfolio source — [github.com/Ollie1994/portfolio](https://github.com/Ollie1994/portfolio) (public), `main` tracking `origin/main`
- [x] **Blazor WebAssembly project** — the frontend (portfolio UI) — `src/Portfolio.Client`
- [x] **Azure Functions project (C#, isolated worker)** — the `/api` backend — `src/Portfolio.Api` (net8.0, one HTTP-triggered `ContactFunction`)
- [x] **Shared class library project** — data models shared between Blazor client and Functions API — `src/Portfolio.Shared`
- [x] **`staticwebapp.config.json`** — routing/navigation fallback + `"apiRuntime": "dotnet-isolated:8.0"`; must live in the Blazor client's `wwwroot/` folder (see reminders below), **not** at the repo root — verified present in `dotnet publish` output
- [x] **`global.json`** at the repo root — pins the SDK to 8.x so a newer SDK installed later can't silently drift the build off .NET 8 — `8.0.100` + `rollForward: latestMinor` (floor, **not** an exact pin: Azure's Oryx build server ships 8.0.420, so pinning an exact local patch version fails CI)
- [x] **Azure Static Web Apps resource** — hosting resource in the Azure Portal (Free SKU), connected to the GitHub repo — East US 2, live at [ambitious-coast-0312a8e0f.7.azurestaticapps.net](https://ambitious-coast-0312a8e0f.7.azurestaticapps.net)
- [x] **GitHub Actions workflow file** — auto-generated on SWA creation; lives in `.github/workflows/` — `azure-static-web-apps-ambitious-coast-0312a8e0f.yml`, deploy verified green

## 5. For building well with Claude Code

- [x] **`CLAUDE.md`** at the repo root — encodes the stack, the .NET 8 / `dotnet-isolated:8.0` hard constraint, solution structure, HTTP-only functions rule, coding conventions, and the exact build/run/test commands
- [x] **Green baseline** — solution skeleton building successfully and committed once before heavy AI work — commit `cbf6684`, build clean (0 warnings, 0 errors), 1/1 test passing
- [x] **Test project (xUnit)** — gives Claude a way to verify logic (also a good CV signal) — `tests/Portfolio.Tests`
- [x] **Small, frequent git commits** — easy review/rollback of AI changes — every change goes through a reviewed pull request, squash-merged

## 6. Optional (CV-relevant)

- [ ] **Custom domain** — from a registrar (~$10–15/yr) if you don't want the default `*.azurestaticapps.net` URL; SSL for it is free from Azure

---

### Key config reminders

- `.csproj` (API): `<TargetFramework>net8.0</TargetFramework>`
- `staticwebapp.config.json`: `"platform": { "apiRuntime": "dotnet-isolated:8.0" }`
- These two version values **must match**.
- `global.json`: `{ "sdk": { "version": "8.0.100", "rollForward": "latestMinor" } }` — keeps `dotnet new` / `dotnet build` on .NET 8 regardless of what else is installed on the machine.
- **`staticwebapp.config.json` location:** it must end up in the *published output* root, so for Blazor WebAssembly it belongs in the client project's `wwwroot/` folder (e.g. `src/Portfolio.Client/wwwroot/`), where the build copies it out. Placed at the repo root it is silently ignored — the app works locally and only breaks on deep-link refresh in production, with the SPA navigation fallback gone.
- Managed Functions on the free tier support **HTTP triggers only** (fine for a contact form or data endpoint; cron/stateful workflows would need the paid Standard plan + "bring your own Functions").

### Deployment gotchas actually hit (first deploy took 3 attempts)

1. **Don't pin `global.json` to an exact patch version.** `dotnet new globaljson --sdk-version`
   writes whatever is installed locally (`8.0.423`). Azure's Oryx build server ships `8.0.420`,
   and `rollForward` only rolls *up*, so the build fails with "A compatible .NET SDK was not
   found". Use a floor (`8.0.100`) instead. It must be a real SDK version: they start at `.100`,
   and `8.0.0` builds locally but fails `setup-dotnet` when it reads `global.json`.
2. **The VS Code extension wrote a Windows backslash** into the generated workflow —
   `api_location: "src\Portfolio.Api"`. The runner is `ubuntu-latest`, so that path never
   resolves. Check the generated workflow for backslashes; `app_location` was fine, only the
   API line was wrong.
3. **`func init` generates worker-level OpenTelemetry that broke the first deploy.** The
   template adds `UseAzureMonitorExporter()` to `Program.cs`, `"telemetryMode":
   "OpenTelemetry"` to `host.json`, and three OpenTelemetry packages. The exporter needs
   `APPLICATIONINSIGHTS_CONNECTION_STRING`, which was absent because Application Insights was not
   yet enabled on the Static Web App — the worker threw on startup and Azure reported only a
   generic "Failed to deploy the Azure Functions". Application Insights has since been enabled,
   so the setting now exists, but the worker-level wiring stays out: the Functions host's own
   integration already forwards the API's logs (see CLAUDE.md; whether request telemetry gets
   through is #59). Strip it for a managed-functions API.

### Free-tier limits to stay under ($0)

- Bandwidth: 100 GB/month (no overage billing — serving pauses if exceeded)
- Storage: 250 MB per environment
- Function executions: 1,000,000 / month
- Custom domains: 2 · Staging environments: 3 · Apps per subscription: 10
