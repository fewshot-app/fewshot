# APEX — Adaptive Personalized EXperience

APEX is a local-first AI middleware that learns your coding patterns, remembers past solutions, and injects personalized context into Claude conversations via the Model Context Protocol (MCP).

All data stays on your machine. No cloud. No subscriptions beyond what you already use.

---

## How it works

APEX sits between you and Claude Desktop. When you start a coding session, the MCP tools pull your preferences, relevant past solutions, and project-specific facts into Claude's context window automatically. At the end of the day (or at 2 AM automatically), Ollama consolidates the conversation into structured memories — things that worked, things that didn't, coding style preferences — and stores them locally for future sessions.

```
You ──► Claude Desktop
             │
             ▼ MCP tools
          APEX API  ──► SQLite (preferences, memories, sessions)
             │
             ▼
          Ollama (nomic-embed-text + qwen3:8b)
             │
          Embeddings + nightly consolidation
```

---

## Prerequisites

**Required:**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Ollama](https://ollama.com) — install and run natively on Windows/Mac/Linux
- [Claude Desktop](https://claude.ai/download)

**Optional:**
- [Presidio Analyzer](https://microsoft.github.io/presidio/) — for enhanced PII detection (Stage 2 of the 3-stage audit pipeline). APEX runs without it; regex + entropy detection still active.

---

## Setup

### 1. Pull Ollama models

```bash
ollama pull nomic-embed-text
ollama pull qwen3:8b
```

Ollama must be running before you start APEX (`ollama serve` if it isn't already).

### 2. Clone and build

```bash
git clone https://github.com/jstarkwv/APEX.git
cd APEX
dotnet build
```

### 3. Start the API

```bash
dotnet run --project src/Apex.Api
```

On first run, APEX automatically creates `%APPDATA%\APEX\apex.db` (SQLite) and runs all migrations. No setup scripts needed.

API is available at `http://localhost:5000`. Swagger UI at `http://localhost:5000/swagger`.

### 4. Seed your projects

Projects drive keyword-based session routing — APEX uses them to automatically associate conversations with the right context. Add them via the Dashboard or the API:

```bash
curl -X POST http://localhost:5000/api/projects \
  -H "Content-Type: application/json" \
  -d '{"name":"myproject","displayName":"My Project","keywords":"myproject, widget, react","facts":null}'
```

Six starter projects are documented in [docs/seed-projects.md](docs/seed-projects.md).

### 5. Configure Claude Desktop MCP

Edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "apex": {
      "command": "C:\\path\\to\\APEX\\src\\Apex.Mcp\\bin\\Release\\net8.0\\Apex.Mcp.exe",
      "args": []
    }
  }
}
```

Build the MCP server first:

```bash
dotnet build src/Apex.Mcp -c Release
```

Restart Claude Desktop. You'll see 7 new tools in the tools menu.

---

## MCP Tools

| Tool | When to use |
|------|-------------|
| `apex_get_context` | Call at the start of every session with a short hint about what you're working on |
| `apex_record_message` | Records messages for nightly consolidation into memories |
| `apex_search_memory` | Semantic search over past solutions |
| `apex_end_day` | Manually close a session and trigger immediate consolidation |
| `apex_list_projects` | List all configured projects |
| `apex_add_project` | Add a new project |
| `apex_remove_project` | Remove a project |

**Example prompt to Claude:**

> Call apex_get_context with hint "wordpress divi modules" before we start.

---

## Dashboard

The Blazor WASM dashboard runs separately:

```bash
dotnet run --project src/Apex.Dashboard
```

Open `http://localhost:5001` (or whatever port is assigned). The dashboard connects to the API at `http://localhost:5000` by default — configure via `src/Apex.Dashboard/wwwroot/appsettings.json`.

**Pages:** Overview · Memories · Preferences · Anti-Patterns · Sessions · Tasks · Projects · Experiments · Audit · Settings

---

## Configuration

`src/Apex.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ApexDb": "Data Source=%APPDATA%\\APEX\\apex.db"
  },
  "Apex": {
    "Ollama": {
      "BaseUrl": "http://127.0.0.1:11434",
      "EmbeddingModel": "nomic-embed-text",
      "SummarizationModel": "qwen3:8b"
    },
    "Presidio": {
      "BaseUrl": "http://127.0.0.1:3000"
    }
  }
}
```

Override any setting with environment variables using the standard .NET pattern:
```
APEX__OLLAMA__BASEURL=http://127.0.0.1:11434
```

---

## How memories work

Every conversation recorded via `apex_record_message` is processed nightly at 2 AM by the consolidation job (Hangfire + Ollama qwen3:8b). It extracts:

- **Memories** — specific solutions and what worked/failed, stored with 768-dim embeddings via nomic-embed-text
- **Preferences** — inferred coding style and tooling preferences with confidence scores that increase each time the same preference is observed
- **Anti-patterns** — things that explicitly failed or caused problems
- **Suggestions** — actionable recommendations from past assistant responses

Semantic search uses cosine similarity computed in-process over the stored embeddings — no external vector DB required.

---

## PII Protection

APEX runs a 3-stage audit pipeline on all context before it's injected into Claude:

1. **Regex** — connection strings, API keys, tokens, SSNs (instant, always on)
2. **Presidio** — names, emails, phones, IPs (optional, requires Presidio sidecar)
3. **Shannon entropy** — catches high-entropy secrets that don't match known patterns

Hard-blocked content (credentials, keys) is dropped silently. PII is redacted in-place. The Audit page in the dashboard lets you test content manually.

---

## Project structure

```
src/
├── Apex.Core/          — Models, interfaces, enums
├── Apex.Infrastructure/ — EF Core (SQLite), services, memory, audit
├── Apex.Api/           — ASP.NET Core Web API, Hangfire jobs, SignalR hub
├── Apex.Dashboard/     — Blazor WASM dashboard
└── Apex.Mcp/           — MCP stdio server for Claude Desktop
tests/
└── Apex.Tests/         — Unit tests
```

---

## Running as a Windows Service (optional)

To have APEX start automatically with Windows:

```bash
dotnet publish src/Apex.Api -r win-x64 --self-contained -o publish/
sc create APEX binPath="C:\path\to\publish\Apex.Api.exe" start=auto
sc start APEX
```

---

## License

MIT
