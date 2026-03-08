# APEX v2.0 — Project Status
**Last Updated:** 2026-03-07

## Overview
APEX (Adaptive Personalized EXperience) is a local-first AI middleware that learns developer preferences, remembers past solutions, and injects personalized context into Claude conversations. All data stays on the local machine — nothing leaves the network.

## Tech Stack
| Component | Technology | Notes |
|-----------|-----------|-------|
| API | .NET 8 Web API | Containerized |
| Database | SQL Server 2025 | Docker, port 1444. Stores all structured data + vector memories |
| Vector Memory | SQL Server `VECTOR(768)` + `VECTOR_DISTANCE()` | Native, no Qdrant |
| Cache / Queue | Redis 7 | AOF persistence, port 6379 |
| Embeddings | Ollama nomic-embed-text | 768-dim cosine, port 11434 |
| Summarization | Ollama qwen3:8b | ~5GB, nightly consolidation |
| PII Detection | Presidio Analyzer | Internal network only, port 3000 |
| Background Jobs | Hangfire | SQL Server storage, /hangfire UI |
| Real-time | SignalR | /hubs/apex |
| MCP Server | Apex.Mcp (.NET 8 stdio) | Claude Desktop integration |
| Dashboard | Blazor WASM | Standalone, runs separately |

## Project Structure
```
C:\Users\Joe\source\repos\APEX\
├── src/
│   ├── Apex.Core/              — Models, Interfaces, Enums
│   ├── Apex.Infrastructure/    — Service implementations
│   │   ├── Audit/              — AuditService (3-stage PII pipeline)
│   │   ├── Context/            — ContextInjector, AclFormatter, ProseFormatter, TokenCounter
│   │   ├── Data/               — ApexDbContext, Migrations/ (001-005)
│   │   ├── Experiments/        — ExperimentService (A/B testing)
│   │   ├── Memory/             — EmbeddingService, MemoryService (SQL Server 2025 VECTOR)
│   │   └── Services/           — Session, Message, Suggestion, Outcome, Preference,
│   │                             AntiPattern, ProjectSession, Task, AgencyGate
│   ├── Apex.Api/               — Web API host
│   │   ├── Controllers/        — Sessions, Messages, Context, Memory, Experiments,
│   │   │                         Audit, Consolidation, Tasks, Projects
│   │   ├── Hubs/               — ApexHub (SignalR)
│   │   ├── Jobs/               — ConsolidationJob, TaskProcessorJob
│   │   └── Program.cs
│   ├── Apex.Dashboard/         — Blazor WASM standalone
│   │   ├── Pages/              — Overview, Memories, Preferences, AntiPatterns,
│   │   │                         Sessions, Tasks, Projects, Experiments, Audit, Settings
│   │   ├── Services/           — ApexApiClient, ApexSignalRService
│   │   └── Shared/             — MainLayout, GateProgress, ThresholdInput
│   └── Apex.Mcp/               — MCP stdio server for Claude Desktop
│       ├── ApexTools.cs        — 7 MCP tools
│       ├── ApexClient.cs       — HTTP wrapper for APEX REST API
│       └── Program.cs
├── docker/
│   ├── docker-compose.yaml     — 5 services (sql, redis, ollama, presidio, api)
│   ├── Dockerfile              — Multi-stage .NET 8 build
│   ├── .env / .env.example
│   ├── init.sh                 — SQL migrations + model pulls
│   ├── migrate-qdrant-to-sql.ps1 — One-time migration script (completed)
│   └── seed-projects.ps1       — Seeds 6 default projects
```

## Docker Services
```bash
cd C:\Users\Joe\source\repos\APEX\docker
docker compose up -d                           # Start all services
docker compose up -d --build apex-api          # Rebuild API only
bash init.sh                                   # First-time setup
powershell -File seed-projects.ps1             # Seed projects
```

**Ports (all bound to 127.0.0.1):**
- 5000 — APEX API + Swagger + Hangfire (/hangfire)
- 1444 — SQL Server 2025
- 6379 — Redis
- 11434 — Ollama

**Timezone:** All containers set to `America/New_York` via `TZ` env var. SQL Server container ignores TZ env; use `AT TIME ZONE 'UTC' AT TIME ZONE 'Eastern Standard Time'` in SSMS queries for local display.

## SQL Migrations
| File | Description |
|------|-------------|
| 001_InitialSchema.sql | Core tables (Sessions, Messages, Preferences, AntiPatterns, etc.) |
| 002_SystemSettings.sql | SystemSettings table |
| 003_SessionProject.sql | Adds Project column to Sessions |
| 004_Projects.sql | Projects table with keyword matching |
| 005_Memories.sql | Memories table with VECTOR(768) column |

## MCP Server (Claude Desktop)
**Location:** `src/Apex.Mcp/bin/Release/net8.0/Apex.Mcp.exe`
**Config:** `C:\Users\Joe\AppData\Roaming\Claude\claude_desktop_config.json`
**Rebuild:** `dotnet build src/Apex.Mcp/Apex.Mcp.csproj -c Release` (quit Claude Desktop first — exe is locked while running)

| Tool | Description |
|------|-------------|
| `apex_get_context` | Resolves project, gets/creates session, returns tiered context |
| `apex_record_message` | Records user/assistant message to active session |
| `apex_search_memory` | Semantic search via SQL Server VECTOR_DISTANCE |
| `apex_end_day` | Closes session, triggers Ollama consolidation |
| `apex_list_projects` | Lists all active projects |
| `apex_add_project` | Adds new project with keywords |
| `apex_remove_project` | Removes project by name |

## Projects (Seeded)
| Name | Display Name | Keywords |
|------|-------------|---------|
| general | General | general, misc, other |
| wordpress | WVU Medicine WordPress | wordpress, divi, wvumedicine, algolia, wvu, plugin, modules, divi5 |
| apex | APEX Middleware | apex, mcp, qdrant, ollama, blazor, middleware, context, injection |
| peakhealth | Peak Health | peakhealth, peak, medicare, hangfire, pdf, itext, provider |
| findadoc | Find a Doc | findadoc, doctors, provider, search, scheduling, dotnet, redirect |
| crunchtime | CrunchTime Gourmet Popcorn | crunchtime, popcorn, trailer, store, inventory |

## Phase Status

### Phase 1: Foundation ✅ COMPLETE
- Core SQL schema (12 tables)
- All CRUD endpoints (Sessions, Messages, Suggestions, Outcomes, Preferences, AntiPatterns)
- Context injection pipeline — 5-tier priority stack (P1-P5) with token budgets
- ACL and Prose formatters with A/B experiment framework
- Token metrics self-recording (ExperimentAssignments.TokensUsed)
- Three-stage audit pipeline (Regex → Presidio HTTP → Shannon entropy)
- Health endpoint, SignalR hub, Hangfire nightly consolidation stub

### Phase 2: Semantic Memory ✅ COMPLETE
- EmbeddingService — Ollama nomic-embed-text, 768-dim
- MemoryService — SQL Server 2025 `VECTOR(768)` + `VECTOR_DISTANCE('cosine')`
  - Replaced Qdrant entirely; 8 memories migrated
- Quality gate — min/max length, duplicate detection (cosine distance ≤ 0.10)
- Memory search with configurable relevance threshold (default 0.55)
- Full CRUD controller + duplicate check endpoint
- Auto-hydration via `POST /api/context/auto`

### Phase 3: Feedback Loop ✅ COMPLETE
- LlmService — Ollama qwen3:8b with JSON extraction + /no_think
- ConsolidationJob — 8-step pipeline extracting memories, anti-patterns, suggestions, preferences
- Quality gate — min 4 messages, 500 chars, max 3 corrections
- ConsolidationController — manual trigger per session or run-all
- **Presidio wired into context injection** — audit gate runs on every segment before sending to Claude
  - Hard block (drop segment): ConnectionString, PrivateKey, BearerToken, AWSKey, GitHubToken, ApiKeyParam, SSN at ≥85% confidence
  - Soft redact (pass through): Presidio PII (names, emails, phones, IPs) — spans replaced with [REDACTED]
  - 50% destruction rule: if redaction removes >50% of segment content, drop segment entirely
  - Dropped segments retained in result for dashboard visibility
  - DATE_TIME and NRP removed from Presidio entity list (too noisy)
  - Blank DetectedType bug fixed in AuditLog logging
- **Preference reinforcement** — `ReinforceOrUpsertAsync` replaces flat upsert in consolidation
  - Same preference seen again → confidence bumps asymptotically: `min(0.95, 0.5 + count * 0.1)`
  - Value changed → confidence drops 0.1 (preference evolved, not confirmed)
  - New preference starts at 0.5, caps at 0.95 (only explicit = 1.0)
  - Consolidation summary now logs `(N reinforced)` count
  - `ReinforceAsync(prefId)` formula also aligned to same curve

### Phase 4: Agency ✅ CORE COMPLETE
- Agency readiness gate — 4 checks (30+ suggestions, 40%+ feedback, 1+ anti-pattern, 5+ sessions)
- TaskService — full lifecycle with state machine and exponential backoff
- Redis task queue — BLPOP with priority/standard queues
- TaskProcessorJob — Hangfire worker (Queued→Analyzing→Executing→Verifying→Completed)
- Approval workflow — tasks pause at AwaitingApproval, resume on approve
- SignalR notifications on every state transition
- [ ] **TODO:** Real task executors (current handlers are placeholders)

### Phase 5: Dashboard ✅ COMPLETE (Blazor WASM)
- Dark theme, sidebar nav, 10 pages
- Overview — stats, agency readiness, recent sessions, service health
- Memories — browse + semantic search, two-step confirm delete, state-sync fix, empty states
- Preferences — grouped by category, inline edit (value + confidence slider + explicit toggle), confirm delete
- Anti-Patterns — error codes, language tags
- Sessions — consolidation status, live SignalR updates with row highlight animation
- Tasks — approval queue, live SignalR updates, dynamic session list (no hardcoded IDs)
- **Projects** — full CRUD: add form (name/displayname/keywords/facts), inline edit, active/inactive toggle, confirm delete
- Experiments — ACL vs Prose token comparison per tier
- Audit — interactive PII scanner (3-stage pipeline)
- Settings — threshold configuration
- **SignalR live updates** — `ApexSignalRService` singleton, one shared connection, auto-reconnect
  - `SessionUpdate` broadcast from ConsolidationJob on complete/fail → Sessions + Overview update live
  - `TaskUpdate` → Tasks + Overview update live
  - "● Live / ○ Offline" indicator on Sessions, Tasks, Memories pages

### Phase 6: MCP Integration ✅ COMPLETE
- `Apex.Mcp` — .NET 8 stdio MCP server
- 7 tools registered and working in Claude Desktop
- Dynamic project resolution via keyword fuzzy matching (Redis-cached sessions, 36hr TTL)
- Projects table — DB-driven, not hardcoded
- stdout corruption fix — `Console.SetOut(Console.Error)` + `SuppressStatusMessages`
- nuget.config scoped to nuget.org only (bypasses WVU Azure DevOps feed)

## ACL vs Prose Experiment Results
| Tier | ACL | Prose | Savings | Winner |
|------|-----|-------|---------|--------|
| P1 Current State | 178 | 213 | 16% | **ACL** |
| P2 Semantic Memory | 252 | 232 | -9% | **Prose** |
| P3 Anti-Patterns | 189 | 209 | 10% | **ACL** |
| P4 Preferences | 147 | 205 | 28% | **ACL** |
| P5 Project Facts | 237 | 285 | 17% | **ACL** |
| **Optimal Hybrid** | **983** | **1144** | **14%** | **Mixed** |

**Recommendation:** ACL for P1/P3/P4/P5, Prose for P2.

## Known Issues & Decisions
1. **nomic-embed-text scores are moderate** — 0.55–0.65 for related content is normal. Search threshold set to 0.55.
2. **P2 better in Prose** — Narrative memory content doesn't compress well in ACL.
3. **Ollama consolidation is CPU-bound** — ~2 min per session. GPU passthrough not yet configured.
4. **SQL Server container ignores TZ env var** — use `AT TIME ZONE` in queries for EST display. Container clock is correct UTC.
5. **ExperimentAssignments unique constraint** — `UQ_Assignment_SessionTier` prevents duplicate tier assignments per session. Use a fresh session ID when testing `/api/context/auto`.
6. **EF table name for AuditLog** — Requires explicit `ToTable("AuditLog")` to avoid EF pluralization mismatch.
7. **PowerShell curl** — Always use `curl.exe`; PowerShell aliases `curl` to `Invoke-WebRequest`.
8. **Docker memory** — Allocate 16GB+ to Docker Desktop to fit qwen3:8b alongside other services.

## Pending TODOs (Priority Order)
1. GPU passthrough for Docker Ollama (performance — consolidation is CPU-bound ~2min/session)
2. Real task executors in Phase 4 agency layer (current handlers are placeholders)
