# APEX v2.0 — Project Status
**Last Updated:** 2026-03-08

---

## Branch Overview

| Branch | Stack | Status |
|--------|-------|--------|
| `main` | SQL Server 2025 + Redis + Docker | Feature-complete, reference architecture |
| `feature/no-docker` | SQLite + in-process + Windows Service | **Active development — install-ready** |

All current work is on **`feature/no-docker`**. This document reflects that branch.

---

## Tech Stack (feature/no-docker)

| Component | Technology | Notes |
|-----------|-----------|-------|
| API | .NET 8 Web API | Windows Service via `sc create` |
| Database | SQLite (EF Core) | `%APPDATA%\APEX\apex.db`, auto-created on startup |
| Vector Memory | EF Core + C# cosine similarity | `byte[]` BLOB embeddings, no external vector DB |
| Cache | `IMemoryCache` | In-process, replaces Redis |
| Queue | `Channel<T>` | In-process, replaces Redis BLPOP |
| Embeddings | Ollama nomic-embed-text | 768-dim, port 11434 |
| Summarization | Ollama qwen3:8b | ~5GB, nightly consolidation |
| PII Detection | Regex + Shannon entropy (built-in) | Always on. Presidio optional sidecar (port 3000) |
| Background Jobs | Hangfire InMemory | No SQL Server dependency |
| Real-time | SignalR | /hubs/apex |
| MCP Server | Apex.Mcp (.NET 8 stdio) | Claude Desktop integration |
| MCP Proxy | Apex.Proxy (.NET 8 stdio) | Wraps any MCP server, scans all tool traffic |
| Dashboard | Blazor WASM | Standalone, port 5001 |

---

## Project Structure

```
C:\Users\Joe\source\repos\APEX\
├── src/
│   ├── Apex.Core/              — Models, Interfaces, Enums
│   │   └── Models/
│   │       ├── ApexPackModels.cs       — Pack import/export models
│   │       ├── LicenseActivationCache.cs
│   │       └── ProxyAuditLog.cs        — Proxy audit findings
│   ├── Apex.Infrastructure/
│   │   ├── Audit/              — AuditService (3-stage PII pipeline)
│   │   ├── Context/            — ContextInjector, formatters, TokenCounter
│   │   ├── Data/               — ApexDbContext (SQLite), no migrations (EnsureCreated)
│   │   ├── Memory/             — EmbeddingService, MemoryService (C# cosine similarity)
│   │   ├── Packs/              — PackCrypto, LicenseApiClient, PackImportService
│   │   └── Services/           — All domain services
│   ├── Apex.Api/               — Web API host
│   │   └── Controllers/        — Sessions, Messages, Context, Memory, Experiments,
│   │                             Audit, Consolidation, Tasks, Projects,
│   │                             PacksController, ProxyAuditController
│   ├── Apex.Dashboard/         — Blazor WASM standalone
│   │   └── Pages/              — Overview, Memories, Preferences, AntiPatterns,
│   │                             Sessions, Tasks, Projects, Experiments, Audit,
│   │                             Packs, Settings
│   ├── Apex.Mcp/               — MCP stdio server (8 tools)
│   └── Apex.Proxy/             — MCP audit proxy (wraps any MCP server)
├── tools/
│   └── Apex.PackTool/          — CLI: apex-pack new/validate/encrypt/decrypt/keygen
├── install.ps1                 — One-line installer (Windows Service + Claude Desktop config)
├── uninstall.ps1
├── README.md                   — Privacy-first positioning
└── STATUS.md                   — This file
```

---

## Phase Status

### Phases 1–6: All Complete ✅
See `main` branch STATUS.md for detailed phase notes. All features ported to SQLite/no-docker.

### Phase 7: Privacy & Distribution ✅ COMPLETE (this branch)

#### 7a — No-Docker Migration ✅
- SQL Server EF provider → SQLite (`Microsoft.EntityFrameworkCore.Sqlite`)
- Redis sessions/cache → `IMemoryCache`
- Redis task queue → `Channel<T>`
- Hangfire.SqlServer → Hangfire.InMemory
- Vector storage: SQL Server `VECTOR(768)` → `byte[]` BLOB + C# cosine similarity
- `%APPDATA%` path expansion in connection string
- `EnsureCreatedAsync` on startup (no migration runner needed)
- Removed docker/ folder entirely

#### 7b — Installer & GitHub Release ✅
- `install.ps1` — checks .NET 8 + Ollama, pulls models, installs Windows Service,
  wires Claude Desktop MCP config, seeds 6 default projects, updates PATH
- `uninstall.ps1` — full teardown, optional data retention
- `.github/workflows/release.yml` — triggered on `v*.*.*` tags, publishes
  win-x64 self-contained single-file exes for Api + Mcp + Dashboard,
  creates GitHub Release with one-liner install command
- **To cut a release:** `git tag v1.0.0 && git push origin v1.0.0`

#### 7c — Pack System ✅
- `Apex.PackTool` CLI — `new`, `validate`, `encrypt`, `decrypt`, `keygen`
- Pack format: AES-256-CBC encrypted JSON envelope with SHA256 integrity hash
- `PackImportService` — activate license → download pack → decrypt → embed → bulk import
- `PacksController` — `POST /api/packs/import` (license key only, no file upload)
- `GET /api/packs/activated`, `GET /api/packs/machine-id`
- `LicenseActivationCache` in SQLite — caches activation so re-imports skip license server
- Dashboard **Packs page** — enter license key, click Import, done
- Pack ID resolved from key prefix (e.g. `APEX-WP-...` → `wordpress-divi`)

#### 7d — APEX.Licensing API ✅ (separate repo)
- Repo: `C:\Users\Joe\source\repos\APEX.Licensing` (to be created from outputs)
- Stack: .NET 8 minimal API + SQLite + Dapper, Docker Compose port 5100
- Cloudflare Zero Trust tunnel → `apex-licenses.lefevrecorpwv.com`
- `POST /licenses/activate` — validates key, checks machine limit (2),
  returns `{ decryptionKey, packUrl }`
- `POST /licenses/deactivate`
- `POST /webhooks/lemonsqueezy` — HMAC-verified, provisions key on `order_created`
- `GET /packs/{filename}` — static file serving of encrypted `.apexpack` files
  from `/packs/` Docker volume
- `GET /licenses/{key}` — admin only (`X-Admin-Key` header)
- Outputs: `/mnt/user-data/outputs/APEX.Licensing/`

#### 7e — MCP Audit Proxy ✅
- `Apex.Proxy` — new project, added to solution
- Wraps any MCP server as a transparent stdio interceptor
- Scans every JSON-RPC message in both directions (outbound to tool, inbound from tool)
- Same 3-stage detection as AuditService: regex patterns + Shannon entropy
- Blocking findings (ConnectionString, BearerToken, JWT, SSN, PrivateKey, CreditCard)
  redacted before forwarding; all findings logged to `POST /api/proxy-audit/log`
- `ProxyAuditLog` model + EF config in `ApexDbContext`
- `ProxyAuditController` — `/api/proxy-audit/log`, `/logs`, `/stats`
- **Does not intercept:** raw conversation text between user and Claude
  (goes direct to `api.anthropic.com` — outside APEX scope)
- Setup: wrap each MCP server entry in `claude_desktop_config.json`

#### 7f — README Rewrite ✅
- Privacy-first positioning (not feature-first)
- Clear "Privacy scope" section — what APEX protects vs. what it doesn't
- Honest about conversation traffic going to Anthropic directly
- Skills comparison table
- Proxy scope documented accurately

---

## Privacy Scope (important for marketing/docs)

### What APEX protects
| Layer | What's protected | How |
|-------|-----------------|-----|
| Context injection | Memories/prefs injected into Claude | 3-stage audit pipeline (Regex → Presidio → Entropy) |
| MCP tool traffic | Arguments sent to tools, results returned | `Apex.Proxy` stdio interceptor |

### What APEX does NOT protect
- **Direct conversation text** — messages you type and Claude's responses go straight
  to `api.anthropic.com` over HTTPS. APEX never sees this.
- **Non-MCP integrations** — browser Claude, direct API calls, other AI clients

### Planned: `apex_scan` MCP tool
- New tool in `Apex.Mcp` that scans content on demand
- Used with a Claude Desktop system prompt to scan messages before sending
- Closes the gap for content typed directly into Claude (partial coverage)
- **Status: not yet built** — estimated 1-2 hours

---

## Competitor Context

| Tool | Protects | Data goes to | Target |
|------|----------|-------------|--------|
| LiteLLM | Company from cost overruns | Cloud models | Enterprise IT |
| Usage Panda | Company from bad prompts | Their servers + cloud | SaaS teams |
| Portkey | Company from downtime | Cloud models | DevOps/SRE |
| GeniusRise | Compliance checkbox | Cloud models | Enterprise compliance |
| Gravitee | Unauthorized access | Cloud models | Enterprise IT |
| **APEX Proxy** | **User from data exposure** | **Local only** | **Privacy-conscious devs** |

Key differentiator: they protect the org from the user. APEX protects the user from the cloud.

---

## Pack Ecosystem

### Packs Available (planned)
| Pack ID | Name | Key Prefix | Price |
|---------|------|-----------|-------|
| `wordpress-divi` | WordPress / Divi Pro Pack | `APEX-WP-` | $12 |
| `dotnet-azure` | .NET / Azure Pro Pack | `APEX-DN-` | $12 |
| `react-ts` | React / TypeScript Pro Pack | `APEX-RE-` | $12 |
| `algolia` | Algolia Search Pack | `APEX-AL-` | $9 |
| `fullstack-bundle` | Full-Stack Bundle (all above) | `APEX-FS-` | $25 |

### Pack workflow
1. Export real memories from APEX instance (export endpoint not yet built)
2. Merge with `sample-packs/wordpress-divi.apexpack.json` template
3. `apex-pack validate` → `apex-pack encrypt --key <DecryptionKey from LicenseKeys table>`
4. Drop encrypted file into `/packs/` volume on licensing server
5. Update `PackFileName` in `LicensePacks` table
6. Create Lemon Squeezy product, add to `ProductPackMap` in `WebhooksController.cs`

---

## MCP Tools

| Tool | Description |
|------|-------------|
| `apex_get_context` | Resolves project, gets/creates session, returns tiered context |
| `apex_record_message` | Records user/assistant message to active session |
| `apex_search_memory` | Semantic search via C# cosine similarity |
| `apex_end_day` | Closes session, triggers Ollama consolidation |
| `apex_list_projects` | Lists all active projects |
| `apex_add_project` | Adds new project with keywords |
| `apex_remove_project` | Removes project by name |
| `apex_scan` | Scans text for PII/secrets via 3-stage audit pipeline |

---

## Projects (Seeded)

| Name | Display Name | Keywords |
|------|-------------|---------|
| general | General | general, misc, other |
| wordpress | WVU Medicine WordPress | wordpress, divi, wvumedicine, algolia, wvu, plugin, modules |
| apex | APEX Middleware | apex, mcp, ollama, blazor, middleware, context, proxy |
| peakhealth | Peak Health | peakhealth, peak, medicare, hangfire, pdf, itext, provider |
| findadoc | Find a Doc | findadoc, doctors, provider, search, scheduling, dotnet |
| crunchtime | CrunchTime Gourmet Popcorn | crunchtime, popcorn, trailer, store, inventory |

---

## Key File Locations

| Item | Path |
|------|------|
| APEX repo | `C:\Users\Joe\source\repos\APEX` (branch: `feature/no-docker`) |
| SQLite DB | `%APPDATA%\APEX\apex.db` (auto-created) |
| Claude Desktop config | `%APPDATA%\Claude\claude_desktop_config.json` |
| APEX.Licensing outputs | `/mnt/user-data/outputs/APEX.Licensing/` |
| Pack tools outputs | `/mnt/user-data/outputs/pack-tools/` |
| Sample WordPress pack | `/mnt/user-data/outputs/pack-tools/sample-packs/wordpress-divi.apexpack.json` |

---

## Pending Work (Priority Order)

1. ~~**`apex_scan` MCP tool**~~ ✅ DONE — scans content via 3-stage audit pipeline (regex, Presidio, entropy)
2. **Pack export endpoint** — `GET /api/packs/export/{project}` to export real memories as pack JSON
3. **APEX.Licensing deploy** — create repo, docker compose up, seed packs, wire Lemon Squeezy
4. **Proxy Audit dashboard page** — visualize `ProxyAuditLog` findings (currently API-only)
5. **GitHub Release** — `git tag v1.0.0 && git push origin v1.0.0` to trigger release workflow
6. **Pack content** — build real wordpress-divi pack from actual WVU Medicine memories
7. **GPU passthrough for Ollama** — consolidation currently CPU-bound (~2min/session)
8. **Real task executors** — Phase 4 agency handlers are still placeholders

---

## Recent Commits (this branch)

```
a9b58d4  docs: rewrite README with privacy-first positioning and accurate proxy scope
e0265d2  feat: add Apex.Proxy MCP audit proxy + ProxyAuditLog + ProxyAuditController
caeccc9  docs: add README and cleanup (previous session)
b5099e7  feat: add install.ps1/uninstall.ps1 + GitHub Actions release workflow
e010d0b  feat: SQLite migration — remove Docker/SQL Server/Redis dependencies
```
