# APEX — Adaptive Personalized EXperience

**Your AI memory. Your machine. Nobody else's.**

APEX is a local-first middleware layer that makes Claude smarter about *you* — your codebase, your patterns, your hard-won lessons — without sending any of it to a cloud service.

---

## The privacy problem with AI tools

Every time you give an AI assistant context about your work — your architecture decisions, your client's codebase, your debugging history — that information leaves your machine. It trains models. It sits on servers you don't control. For developers working on proprietary systems, healthcare software, financial applications, or anything under an NDA, that's not acceptable.

Claude has a feature called **Skills** that lets you upload documents for Claude to reference. It's useful. But those documents live on Anthropic's servers, they're included in your context window on every message, and they're static — they don't learn from what you do.

**APEX is the private alternative.**

- Your memories, preferences, and learned patterns stay in a SQLite database on your own machine
- Nothing is uploaded to any cloud service — not Anthropic's, not anyone's
- Context is injected into Claude via MCP on your local network — it never leaves your machine
- APEX gets smarter over time from your actual work, not from a document you wrote once

---

## What APEX does

APEX sits between you and Claude Desktop via the Model Context Protocol. When you start a coding session, it automatically injects relevant context — past solutions, your coding preferences, project-specific facts, things that previously failed — directly into Claude's awareness. At the end of the day, Ollama (also running locally) consolidates the conversation into structured memories for future sessions.

```
You ──► Claude Desktop
             │
             ▼  MCP (local only)
          APEX API
             │
       ┌─────┴──────┐
       │            │
   SQLite DB     Ollama
  (your machine) (your machine)
  preferences    nomic-embed-text
  memories       qwen3:8b
  anti-patterns
```

Everything in that diagram runs on your hardware. The only outbound connection APEX makes is to Ollama's embedding and summarization models — which are also running locally.

---

## How it compares to Claude Skills

| | Claude Skills | APEX |
|---|---|---|
| **Data location** | Anthropic's servers | Your machine |
| **Privacy** | Subject to Anthropic's data policy | Fully private |
| **Content** | Static documents you upload | Living memory built from real work |
| **Context usage** | Included every message | Injected only when semantically relevant |
| **Learns over time** | No | Yes — reinforces patterns from actual usage |
| **Works across clients** | Claude.ai only | Any MCP-compatible client |
| **Cost** | Counts against context window | Selective injection, minimal overhead |

---

## Privacy scope — what APEX protects and what it doesn't

This is important to understand clearly.

### What APEX protects

**Context injection** — Everything APEX injects into Claude (memories, preferences, project facts) passes through a 3-stage audit pipeline before it reaches Claude. Connection strings, API keys, tokens, PII, and high-entropy secrets are redacted or blocked. Your past work never leaks back to Claude in raw form.

**MCP tool traffic** — With `Apex.Proxy` (see below), every argument passed to any MCP tool and every response returned by any MCP tool is scanned before forwarding. If a filesystem MCP server reads a file containing a database password and tries to return it, the proxy catches it before Claude sees it.

### What APEX does not protect

**Your direct conversation with Claude** — The messages you type and Claude's responses travel directly from Claude Desktop to `api.anthropic.com` over HTTPS. APEX has no visibility into this traffic. Your conversation is subject to [Anthropic's standard data policy](https://www.anthropic.com/privacy).

**Non-MCP integrations** — Browser-based Claude usage, the Claude API called directly by other tools, or any AI interaction that doesn't go through Claude Desktop's MCP layer is outside APEX's scope.

The practical implication: be mindful of what you paste directly into the Claude chat window. APEX protects what flows through MCP tools — it doesn't protect what you type.

---

## Apex.Proxy — MCP Audit Proxy

`Apex.Proxy` is an optional component that wraps any MCP server and scans all traffic before it reaches Claude or the MCP server. It's useful when you're running third-party MCP servers (filesystem, databases, browser automation) that may return sensitive data.

### What it intercepts

Every JSON-RPC message flowing through the MCP stdio transport in both directions:

- **Outbound** (Claude → MCP server): tool call arguments — what Claude is asking the tool to do
- **Inbound** (MCP server → Claude): tool results — what the tool returns back to Claude

Each message is scanned for:
- Connection strings and database credentials
- Bearer tokens and JWTs
- Private keys
- AWS/GitHub/API keys
- SSNs and credit card numbers
- High-entropy secrets (Shannon entropy analysis)

Blocking findings are redacted before forwarding. All findings are logged to the APEX API at `/api/proxy-audit/logs`.

### What it does not intercept

- The conversation itself (your messages and Claude's responses)
- HTTPS traffic to `api.anthropic.com`
- MCP servers not configured to run through the proxy

### Setup

Build the proxy:

```bash
dotnet build src/Apex.Proxy -c Release
```

Wrap each MCP server in `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "apex": {
      "command": "C:\\...\\Apex.Proxy.exe",
      "args": ["--server", "C:\\...\\Apex.Mcp.exe"]
    },
    "filesystem": {
      "command": "C:\\...\\Apex.Proxy.exe",
      "args": ["--server", "npx", "--", "@modelcontextprotocol/server-filesystem", "C:\\Users\\Joe"]
    }
  }
}
```

One proxy instance per MCP server. Each instance logs findings independently to APEX.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Ollama](https://ollama.com) — runs locally on Windows, Mac, Linux
- [Claude Desktop](https://claude.ai/download)

---

## Install

```powershell
irm https://raw.githubusercontent.com/jstarkwv/APEX/feature/no-docker/install.ps1 | iex
```

The installer checks prerequisites, pulls Ollama models, installs as a Windows Service, and wires up Claude Desktop automatically.

---

## Manual setup

### 1. Pull Ollama models

```bash
ollama pull nomic-embed-text
ollama pull qwen3:8b
```

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

Creates `%APPDATA%\APEX\apex.db` on first run. No migrations needed.

### 4. Configure Claude Desktop

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

Restart Claude Desktop. APEX tools appear in the tools menu.

---

## Using APEX with Claude

Start every session by telling Claude what you're working on:

> *Call apex_get_context with hint "wordpress divi modules" before we start.*

APEX pulls relevant memories, preferences, and project facts into context. Work normally — APEX records the session in the background. At 2 AM the consolidation job turns it into structured memories for next time.

### MCP Tools

| Tool | Purpose |
|------|---------|
| `apex_get_context` | Pull relevant context at session start |
| `apex_record_message` | Record a message for nightly consolidation |
| `apex_search_memory` | Semantic search over past solutions |
| `apex_end_day` | Close session and trigger immediate consolidation |
| `apex_list_projects` | List configured projects |
| `apex_add_project` | Add a new project |
| `apex_remove_project` | Remove a project |

---

## Dashboard

```bash
dotnet run --project src/Apex.Dashboard
```

Open `http://localhost:5001` to browse memories, preferences, anti-patterns, sessions, projects, and proxy audit logs.

---

## Packs

Pre-built collections of memories, preferences, and anti-patterns for specific stacks — built from real production work. Enter a license key in the **Packs** page of the Dashboard. APEX downloads and imports everything automatically.

Available packs:
- WordPress / Divi Pro Pack
- .NET / Azure Pro Pack
- React / TypeScript Pro Pack
- Algolia Search Pack
- Full-Stack Bundle

Pack content is encrypted, licensed per machine, and decrypted locally. It never touches any cloud service on your end.

---

## How memory works

Every session recorded via `apex_record_message` is processed nightly by the consolidation job (Hangfire + Ollama qwen3:8b). It extracts:

- **Memories** — specific solutions and outcomes, stored as 768-dim embeddings via nomic-embed-text
- **Preferences** — coding style and tooling preferences with confidence scores that strengthen each time the same pattern recurs
- **Anti-patterns** — things that explicitly failed
- **Suggestions** — actionable recommendations from past sessions

Semantic search uses cosine similarity computed in-process. No external vector database required.

---

## PII Protection pipeline

APEX runs a 3-stage audit on all context before injection:

1. **Regex** — connection strings, API keys, tokens, SSNs (always on, instant)
2. **Presidio** *(optional)* — names, emails, phones, IPs via local NLP sidecar
3. **Shannon entropy** — high-entropy secrets that don't match known patterns

Hard-blocked content is dropped silently. PII is redacted in-place. The Audit page lets you test content manually.

To run the optional Presidio sidecar:

```bash
docker run -p 3000:3000 mcr.microsoft.com/presidio-analyzer:latest
```

---

## Running as a Windows Service

```bash
dotnet publish src/Apex.Api -r win-x64 --self-contained -o publish/
sc create APEX binPath="C:\path\to\publish\Apex.Api.exe" start=auto
sc start APEX
```

---

## Project structure

```
src/
├── Apex.Core/           — Models, interfaces, enums
├── Apex.Infrastructure/ — EF Core (SQLite), services, memory, audit
├── Apex.Api/            — ASP.NET Core API, Hangfire, SignalR
├── Apex.Dashboard/      — Blazor WASM dashboard
├── Apex.Mcp/            — MCP stdio server for Claude Desktop
└── Apex.Proxy/          — MCP audit proxy (wraps any MCP server)
```

---

## License

MIT
