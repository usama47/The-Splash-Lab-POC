# The Splash Lab — AI and Business Systems Foundation POC

Local proof of concept for the meeting-to-task pilot: ASP.NET Core 8 Web API + Next.js (TypeScript) + SQLite/EF Core. **MODE A (local demo) works with zero external credentials.**

## Architecture

```
Meeting transcript (seed / upload / paste)
        ↓
IMeetingSource (local)
        ↓
IAiExtractionService (Mock default | optional Claude)
        ↓
Validated draft (not published)
        ↓
Human review (Reviewer / Admin)
        ↓
Publish service → Decisions + Commitments + ITaskDestination
        ↓
IAuditLogService (append-only) + status tracking
```

### Project structure

```
TheSplashLab.Poc/
  backend/
    src/TheSplashLab.Core/           # Entities, enums, interfaces, models
    src/TheSplashLab.Infrastructure/ # EF Core, seed, mock/Claude AI, adapters
    src/TheSplashLab.Api/            # Controllers, demo auth, Swagger
    tests/TheSplashLab.Tests/        # xUnit workflow tests
  frontend/                          # Next.js App Router UI
  .env.example
  README.md
```

### Key interfaces

- `IMeetingSource`
- `IAiExtractionService`
- `IKnowledgeRepository`
- `IRegisterRepository`
- `ITaskDestination`
- `IAuditLogService`

## Prerequisites

- .NET 8 SDK
- Node.js 18+ and npm

## Local setup

```bash
# Backend
cd backend
dotnet restore
dotnet build
dotnet test

# Frontend
cd ../frontend
cp .env.local.example .env.local   # if needed
npm install
npm run build
```

### Database

SQLite is created automatically on API startup (`EnsureCreated` + seed). Default path:

`backend/src/TheSplashLab.Api/Data/splashlab-poc.db`

Override with `Database__Path` / `Database:Path`. No separate migration step is required for this POC.

## Start commands

```bash
# Terminal 1 — API (http://localhost:5080)
cd backend/src/TheSplashLab.Api
dotnet run --urls http://localhost:5080

# Terminal 2 — UI (http://localhost:3000)
cd frontend
npm run dev
```

- UI: http://localhost:3000  
- API: http://localhost:5080  
- Swagger: http://localhost:5080/swagger  

## Deploy / publish for a client demo

See **[deploy.md](./deploy.md)** for:

- Same-day local + tunnel sharing
- Hosted split (Render/Railway API + Vercel UI)
- Docker Compose on a VPS (`deploy/`)
- Azure-shaped option
- CORS, SQLite volume, and smoke-check notes

## MODE A vs MODE B

| Integration | MODE A (default) | MODE B |
|---|---|---|
| Meeting source | Seeded / upload (simulated Teams) | Graph interfaces only — **Not Connected** until verified |
| AI extraction | Deterministic **MockAi** | Set `Ai__Provider=Claude` + `ANTHROPIC_API_KEY` |
| Tasks | Local SQLite task repository (simulated) | Same adapter pattern; replace `ITaskDestination` later |
| Acumatica / HubSpot / Shopify | **Not Connected** | Still out of scope |

External AI is **off by default**. The UI labels simulated integrations clearly.

### Enable Claude (optional)

```bash
export Ai__Provider=Claude
export ANTHROPIC_API_KEY=sk-ant-...
dotnet run --urls http://localhost:5080
```

If the key is missing, keep `Ai__Provider=Mock`.

### Microsoft Graph / SharePoint / Lists

Configuration placeholders exist (`MicrosoftGraph:*`). This POC **does not invent Graph permissions or call Graph APIs**. Before production activation, register an Entra app and **verify required permissions from current Microsoft documentation**, then implement against `IMicrosoftGraphIntegration` / a real `IMeetingSource` or Lists-backed `ITaskDestination`.

Demo identity switching is **not** Microsoft Entra authentication.

## Seeded scenarios

1. **Complete meeting** — UK Sample Coordination — clear decision, two owned commitments with due dates.  
2. **Ambiguous meeting** — Product Sample Review — missing owner/due date → Needs Review.  
3. **Fail destination** — Project Kickoff — reject weak ERP suggestion; `FAIL_DESTINATION` exercises failed task write + safe retry.  
4. **Prior published (seed)** — US Warehouse Sync — already in Decisions / Commitments so registers look lived-in at demo start.

Also seeded: 6 processes, 4 issues, 3 knowledge articles, integration statuses, audit history.

## Demo identities (RBAC)

| User | Role |
|---|---|
| Alex Morgan | Employee |
| Jordan Lee | Reviewer (default) |
| Sam Patel | Administrator |

Switch via the sidebar control (local demo only).

## 10-minute demo walkthrough

Full script: **[docs/demo-walkthrough.md](./docs/demo-walkthrough.md)**  

Short version:

1. Dashboard — MODE A banner + seeded counts / workflow progress.  
2. Meetings → **UK Sample Coordination** → Generate Draft → approve → publish.  
3. Decisions + Commitments — published rows + `LOCAL-…` simulated tasks; try status update.  
4. **Product Sample Review** — Needs Review → fill owner/date → publish.  
5. Optional: Employee cannot publish (403).  
6. **Project Kickoff** — reject ERP / or show FAIL_DESTINATION failure + retry.  
7. Process Register, Issues → Knowledge approve, Integration Settings.  
8. Audit Log + restart API to prove SQLite persistence.

## Proposed 90-day growth path (not delivered by this POC)

- **Days 1–30:** Foundation and pilot workflow (this POC).  
- **Days 31–60:** Department adoption, wider process mapping, validated source-system access.  
- **Days 61–90:** Priority process improvements, approved knowledge reuse, recurring scorecards.

## Limitations / production rollout needs

- No real Teams/Graph/SharePoint sync.  
- No Acumatica, HubSpot, or Shopify connectivity.  
- Demo auth is header-based local switching, not Entra ID / cross-tenant auth.  
- Claude optional; mock is the supported zero-credential path.  
- Not a replacement ERP/CRM/commerce platform or Ninety.io.  
- Production needs: proper identity, verified Graph permissions, secrets management, hardened hosting, and operational monitoring.

## Tests

```bash
cd backend && dotnet test
```

Covers transcript validation, AI schema validation, missing owner/due date, unauthorized approval, approve/reject handling, task creation after approval, idempotent publish, destination failure/retry, audit recording, overdue status, and knowledge approval gate.
