# Serverless C#/.NET Portfolio — Setup Checklist

**Stack:** Blazor WebAssembly (C#) · Azure Static Web Apps (Free tier) · Azure Functions (C#, isolated) · GitHub Actions CI/CD · VS Code

**Target framework:** .NET 8 (LTS) — required for Static Web Apps managed-Functions compatibility (do NOT use .NET 10 for the API; SWA does not support it yet).

---

## 1. Accounts

- [ ] **Microsoft / Azure account** — with an active Azure subscription (free one works; payment card required on file, but the Static Web App itself won't bill) — *not verifiable from this machine; Azure CLI isn't installed. Confirm manually.*
- [x] **GitHub account** — hosts the repo and runs CI/CD — signed in as `Ollie1994` via `gh`
- [x] **Anthropic account** — for signing into Claude Code (Claude Pro/Max plan or API credits)

## 2. Software to download & install

- [x] **VS Code** — the editor
- [x] **.NET 8 SDK (LTS)** — the framework/build tooling; target .NET 8 to stay SWA-compatible — `8.0.423`
- [x] **Node.js (LTS)** — prerequisite for the Claude Code CLI and SWA CLI (no JS coding needed) — `v22.14.0`
- [x] **Git** — version control + GitHub connection
- [x] **Azure Functions Core Tools** — local runtime to run/debug C# Functions — `4.12.1`
- [x] **Azure Static Web Apps CLI (SWA CLI)** — emulates the real Azure environment locally (frontend + API on one address) — `2.0.10`
- [x] **Claude Code CLI** — agentic coding tool
- [x] **Modern browser (Chrome/Edge)** — for running/debugging the Blazor WASM app — both installed

## 3. VS Code extensions

- [x] **C# Dev Kit** (Microsoft) — core C#/.NET experience — `v3.20.199`
- [x] **C#** extension — installed as a dependency of C# Dev Kit — `v2.140.9`
- [x] **Azure Static Web Apps** extension — create/deploy tooling — `v0.13.3`
- [x] **Azure Functions** extension — local run/debug + deploy for the API — `v1.22.0`
- [x] **Azure Resources / Azure Account** extension — sign-in and resource management — `v0.12.7`
- [x] **Claude Code** extension — native integration (panel, inline diffs, auto context sharing)

## 4. Things to create

- [ ] **GitHub repository** — for the portfolio source — *`git init` done locally (branch `main`), but no remote and no commits yet*
- [x] **Blazor WebAssembly project** — the frontend (portfolio UI) — `src/Portfolio.Client`
- [x] **Azure Functions project (C#, isolated worker)** — the `/api` backend — `src/Portfolio.Api` (net8.0, one HTTP-triggered `ContactFunction`)
- [x] **Shared class library project** — data models shared between Blazor client and Functions API — `src/Portfolio.Shared`
- [x] **`staticwebapp.config.json`** — routing/navigation fallback + `"apiRuntime": "dotnet-isolated:8.0"`; must live in the Blazor client's `wwwroot/` folder (see reminders below), **not** at the repo root — verified present in `dotnet publish` output
- [x] **`global.json`** at the repo root — pins the SDK to 8.x so a newer SDK installed later can't silently drift the build off .NET 8 — `8.0.423`, `rollForward: latestMinor`
- [ ] **Azure Static Web Apps resource** — hosting resource in the Azure Portal (Free SKU), connected to the GitHub repo
- [ ] **GitHub Actions workflow file** — auto-generated on SWA creation; lives in `.github/workflows/`

## 5. For building well with Claude Code

- [x] **`CLAUDE.md`** at the repo root — encodes the stack, the .NET 8 / `dotnet-isolated:8.0` hard constraint, solution structure, HTTP-only functions rule, coding conventions, and the exact build/run/test commands
- [ ] **Green baseline** — solution skeleton building successfully and committed once before heavy AI work — *builds clean (0 warnings, 0 errors) and 1/1 test passes, but **nothing is committed yet***
- [x] **Test project (xUnit)** — gives Claude a way to verify logic (also a good CV signal) — `tests/Portfolio.Tests`
- [ ] **Small, frequent git commits** — easy review/rollback of AI changes — *no commits yet*

## 6. Optional (CV-relevant)

- [ ] **Custom domain** — from a registrar (~$10–15/yr) if you don't want the default `*.azurestaticapps.net` URL; SSL for it is free from Azure

---

### Key config reminders

- `.csproj` (API): `<TargetFramework>net8.0</TargetFramework>`
- `staticwebapp.config.json`: `"platform": { "apiRuntime": "dotnet-isolated:8.0" }`
- These two version values **must match**.
- `global.json`: `{ "sdk": { "version": "8.0.0", "rollForward": "latestMinor" } }` — keeps `dotnet new` / `dotnet build` on .NET 8 regardless of what else is installed on the machine.
- **`staticwebapp.config.json` location:** it must end up in the *published output* root, so for Blazor WebAssembly it belongs in the client project's `wwwroot/` folder (e.g. `src/Portfolio.Client/wwwroot/`), where the build copies it out. Placed at the repo root it is silently ignored — the app works locally and only breaks on deep-link refresh in production, with the SPA navigation fallback gone.
- Managed Functions on the free tier support **HTTP triggers only** (fine for a contact form or data endpoint; cron/stateful workflows would need the paid Standard plan + "bring your own Functions").

### Free-tier limits to stay under ($0)

- Bandwidth: 100 GB/month (no overage billing — serving pauses if exceeded)
- Storage: 250 MB per environment
- Function executions: 1,000,000 / month
- Custom domains: 2 · Staging environments: 3 · Apps per subscription: 10
