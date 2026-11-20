# The Splash Lab POC — 10-minute client demo script

**Audience:** CEO / stakeholder walkthrough of the Days 1–30 foundation pilot  
**Mode:** MODE A — local demo, simulated integrations, zero external credentials  

| | URL |
|---|---|
| UI | http://localhost:3000 |
| API | http://localhost:5080 |
| Swagger | http://localhost:5080/swagger |

## Start the apps

```bash
# Terminal 1 — API
cd backend/src/TheSplashLab.Api
dotnet run --urls http://localhost:5080

# Terminal 2 — UI
cd frontend
npm run dev
```

**Rehearse again:** on **Meetings**, click **Start a fresh demo scenario** (Reviewer/Admin only, asks to confirm).  
It does **not** reset the whole database. It removes pasted/uploaded meetings (and any old `[Rehearsal N]` copies), plus the drafts and decisions/commitments/tasks/issues created from meetings, and sets the three scenario meetings back to **New**. The prior published meeting, other registers and the audit trail are kept (the reset itself is logged).

Full wipe (local only, last resort): stop the API, delete `backend/src/TheSplashLab.Api/Data/splashlab-poc.db*`, restart.

**Pre-demo check (hosted UI):** the UI's `NEXT_PUBLIC_API_BASE` must point at the API, and the UI origin must be in the API's `Cors:AllowedOrigins`. Open the Dashboard and confirm counts load (no CORS/network error) — see `deploy.md`.

**Identity:** stay as **Jordan Lee (Reviewer)** in the left sidebar unless demonstrating RBAC.  
Sidebar note: switching is **local demo only — not Microsoft Entra**.

---

## What is already seeded (before you click anything)

| Area | Seeded content |
|---|---|
| Meetings | **3 scenario meetings (New)** + **1 prior published** meeting |
| Decisions | 1 approved decision from the prior warehouse meeting |
| Commitments / tasks | Prior commitments with `LOCAL-…` task IDs (Simulated destination) |
| Issues | 4 issues (open / in progress / resolved / critical) |
| Knowledge | 3 articles (2 approved, 1 pending approval) |
| Process register | 6 processes across Automate / Accelerate / Elevate |
| Integrations | Simulated (Teams source, Mock AI, local tasks) + Not Connected (Graph, Acumatica, HubSpot, Shopify) |
| Audit | Seed + prior publish / status history |

**Scenario meetings to run live**

1. `complete` — **UK Sample Coordination — Quotation Follow-up**  
2. `ambiguous` — **Product Sample Review — Architect Feedback**  
3. `fail-destination` — **Project Kickoff — Destination Adapter Stress Case**  

Already published (for lived-in registers):  
`prior-published` — **US Warehouse Sync — Finish Availability**

Hosting options for sharing outside localhost: see repo root **`deploy.md`**.

---

## Minute-by-minute script

### 0:00–1:00 — Set the frame (Dashboard)

1. Open **Dashboard**.  
2. Point out the top banner: **MODE A — Local demo · Simulated integrations**.  
3. Show live counts (prior published work already visible — meetings processed, decisions, processes mapped, etc.).  
4. Show **Meeting-to-task workflow progress** (New / Draft / In Review / Published).  
5. Say in one sentence:  
   > “One department (Operations), one complete meeting-to-task workflow, human approval before anything becomes an official record.”

### 1:00–3:00 — Scenario 1: Complete meeting

1. Go to **Meetings**.  
2. Open **UK Sample Coordination — Quotation Follow-up** (`complete`).  
3. Show the **Simulated Teams Transcript** and readable source text.  
4. Click **Generate Draft (AI)**.  
5. Call out:  
   - Badge **Demo AI (simulated)**  
   - **AI suggestions are not official records**  
   - Clear decision + **two commitments with named owners and due dates**  
6. Approve the decision and both commitments.  
7. **Save draft** → **Approve & publish**.  
8. Confirm success (no destination failure on this scenario).

### 3:00–5:00 — Registers & follow-up

1. **Decisions** — Riverside matte-black sample decision **plus** the prior warehouse decision.  
2. **Commitments and Tasks** — new rows with `LOCAL-…` IDs and **Simulated** destination label.  
3. Click **In progress** then **Complete** on one new commitment (status updates must succeed — no server error).  
4. Note: **Overdue** is computed from due dates when applicable (not a fake label).

### 5:00–7:00 — Scenario 2: Ambiguous meeting

1. **Meetings** → **Product Sample Review — Architect Feedback** (`ambiguous`).  
2. **Generate Draft**.  
3. Show orange **Needs Review** on the commitment (owner / due date missing) and the reason text.  
4. As reviewer: fill owner + due date → Approve → **Save draft** → **Approve & publish**.  
5. Optional RBAC beat: switch to **Alex Morgan (Employee)** and try publish → **forbidden**; switch back to Jordan.

### 7:00–8:00 — Scenario 3: Reject / failed destination

1. Open **Project Kickoff — Destination Adapter Stress Case** (`fail-destination`).  
2. **Generate Draft**.  
3. Path A (clean reject): **Reject** the imaginary ERP / `FAIL_DESTINATION` action; approve only the valid checklist commitment; publish. Confirm rejected action **never** appears in Commitments.  
4. Path B (optional failure demo): approve the `FAIL_DESTINATION` item to show a **failed** simulated task, visible failure reason, and safe **Retry failed tasks** (idempotent, no duplicates).

### 8:00–9:00 — Knowledge, process baseline, integrations

1. **Process Register** — show Automate / Accelerate / Elevate hour totals (6 mapped processes).  
2. **Issues** — open the resolved sample-lead-time issue → **Propose knowledge**.  
3. **Knowledge** — approve the pending draft; remind: drafts do not auto-approve.  
4. **Integration Settings** — Simulated vs **Not Connected** (Graph / Acumatica / HubSpot / Shopify). No invented live ERP/CRM feeds.

### 9:00–10:00 — Audit + persistence

1. **Audit Log** — show GenerateDraft / SaveDraft / PublishDraft / status / knowledge actions (append-only).  
2. Restart the API process; reopen Dashboard — published decisions/commitments still present (SQLite under `Data/`).  
3. Close:  
   > “This is the Days 1–30 foundation — not the full 90-day program, and not a replacement for Acumatica, HubSpot, Shopify, or production Entra auth.”

---

## Presenter tips

- Keep the sidebar identity on **Reviewer** for the happy path.  
- If registers look empty or odd, reseed by deleting the SQLite files and restarting the API.  
- If the UI cannot reach the API, confirm `NEXT_PUBLIC_API_BASE=http://localhost:5080` and that the API is on `:5080`.  
- For a remote client link, follow **`deploy.md`** (tunnel, Render/Vercel, Docker, or Azure).  
- Do not claim Graph / Acumatica / HubSpot / Shopify are live.

## 90-day framing (say this; do not claim it is built)

- **Days 1–30:** foundation + pilot workflow *(this POC)*  
- **Days 31–60:** department adoption, wider process mapping, validated source-system access  
- **Days 61–90:** priority process improvements, approved knowledge reuse, recurring scorecards  
