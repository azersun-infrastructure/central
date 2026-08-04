# OpsCentral

Open-source replacement for the closed-source "Management Cockpit" internal admin tool.
MVP scope: the **User Management** module (Unlock / Disable / Enable / Search / Send Locked /
Sync AD), backed by an audit log ("Request Result Table"). Other sidebar sections (M365,
Expired users, Create User, Password Management, Package Deployment) are placeholders for
future phases.

Stack: ASP.NET Core 8 (Blazor Server), EF Core (SQLite for dev / PostgreSQL for prod), Entra ID
SSO (Microsoft.Identity.Web) with a local fallback admin account, Microsoft Graph SDK (app-only),
and a Jenkins/Azure Automation job-dispatch abstraction for all AD operations. See
`c:\Users\Taryel.Kazimov\.claude\plans\ok-m-n-tam-opensource-enumerated-castle.md` for the full
design.

## Running locally (no Docker)

```bash
cd OpsCentral
export OPSCENTRAL_LOCAL_ADMIN_USERNAME=admin
export OPSCENTRAL_LOCAL_ADMIN_PASSWORD='choose-a-strong-password'
dotnet run
```

- Uses SQLite (`opscentral.db`) and the mock dispatcher (`Dispatch:UseMock=true` in
  `appsettings.Development.json`) — no real Jenkins/Azure Automation/Entra app registration
  needed to try the app end-to-end.
- Log in at `/Account/LocalLogin` with the seeded credentials above. `AzureAd:TenantId`/`ClientId`
  in `appsettings.Development.json` are throwaway placeholder GUIDs so the app can start without
  a real Entra app registration — "Sign in with Entra ID" won't work until you configure a real one.

## Running via Docker Compose

```bash
cp .env.example .env   # fill in real secrets
docker compose up --build
```

Runs the app against PostgreSQL. `docker-compose.override.yml` (applied automatically) sets
`ASPNETCORE_ENVIRONMENT=Development` and exposes Postgres on `5432` for local inspection.

## Configuration

All secrets are read from environment variables / `dotnet user-secrets` — never commit real
values into `appsettings*.json`. See `.env.example` for the full list and `appsettings.json`
for the config shape (Jenkins job names, Azure Automation webhook URLs, AD action routing, etc).

## Tests

```bash
dotnet test
```
