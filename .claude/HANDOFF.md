# Handoff — Splash Lab POC

## Goal
Reviewer-flow work (inline publish edits + conflict, deterministic seed dates, Scenario 3 retry + results panel, Process Register worked example). **Code is written; awaiting user's build/test results.** Do not open a PR unless asked. Print build/test commands for the user (CLAUDE.md rule 7).

## Stack
ASP.NET Core 8 + EF Core SQLite (`EnsureCreated`, no migrations), Next.js 14 (plain CSS), xUnit tests in `backend/tests/TheSplashLab.Tests/WorkflowTests.cs`.

## Done (uncommitted, unverified — never compiled)
- Core/Models/ExtractionModels.cs: `ExpectedVersion`, `PublishRequest.Draft`, richer `PublishResult` (TasksNewlyCreated/AlreadyExisting, Tasks list), `PublishedTaskDto`, `DraftConflictException`.
- Infrastructure/Services/MeetingWorkflowService.cs: `ApplyDraftEdits`, `EnsureVersion`/`VersionOf` (UpdatedAtUtc ticks), publish applies inline edits, scenario-3 fails once (first attempt only, action == `ScenarioTranscripts.FailOnceAction`), audit action `RetryFailedTasks` when ForceRetry.
- Infrastructure/Seed/DemoSeed.cs: `ScenarioTranscripts` now public with shared date consts; Scenario 3 transcript rewritten (two approved actions, runbook due 24 Nov 2026).
- Infrastructure/Ai/MockAiExtractionService.cs: Complete + FailDestination payloads use the consts.
- Api/Controllers/MeetingsController.cs: `Version` in draft DTO; 409 + `currentDraft` on conflict.
- Tests: replaced two old Scenario-3 tests (+ removed `ScenarioMini`) with 5 new ones (inline edits, reject-after-approve, stale-version conflict, evidence/date stability theory, scenario-3 retry).
- Frontend: `lib/api.ts` (`ApiError`, types), `meetings/[id]/page.tsx` (inline publish, conflict panel, results panel + Retry), `commitments` (hash highlight), `audit` (?meetingId filter), `processes` (sample time/cost panel).
- Fonts: system-ui stack (layout.tsx/globals.css), local only.

## Next step
User runs commands below and pastes failures; fix compile/test errors only. Then offer PR description (results + click-through demo script).

## Commands for you to run
```powershell
cd backend; dotnet build; dotnet test
cd ../frontend; npm run build
```
Local DB must be deleted once (`backend/src/TheSplashLab.Api/Data/splashlab-poc.db`) to pick up the new Scenario 3 seed; Render reseeds itself.

## Gotchas
- Deploy: API https://the-splash-lab-poc.onrender.com, UI https://the-splash-lab-poc.vercel.app (Vercel builds a copy repo under MuhammadKhan3; `origin` is usama47). Render free resets DB on restart.
- Not requested/not done: "Start a fresh demo scenario" (dropped from the latest brief).
