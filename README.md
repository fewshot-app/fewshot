# Fewshot — Context packs & memory for AI agents

**Your AI memory. Your machine. Nobody else's.**

Fewshot is a local-first middleware layer that makes Claude smarter about *you* — your codebase, your patterns, your hard-won lessons — without sending any of it to a cloud service.

---

## The privacy problem with AI tools

Every time you give an AI assistant context about your work — your architecture decisions, your client's codebase, your debugging history — that information leaves your machine. It trains models. It sits on servers you don't control. For developers working on proprietary systems, healthcare software, financial applications, or anything under an NDA, that's not acceptable.

Claude has a feature called **Skills** that lets you upload documents for Claude to reference. It's useful. But those documents live on Anthropic's servers, they're included in your context window on every message, and they're static — they don't learn from what you do.

**Fewshot is the private alternative.**

- Your memories, preferences, and learned patterns stay in a SQLite database on your own machine
- Nothing is uploaded to any cloud service — not Anthropic's, not anyone's
- Context is injected into Claude via MCP on your local network — it never leaves your machine
- Fewshot gets smarter over time from your actual work, not from a document you wrote once

---

## What Fewshot does

Fewshot sits between you and Claude Desktop via the Model Context Protocol. When you start a coding session, it automatically injects relevant context — past solutions, your coding preferences, project-specific facts, things that previously failed — directly into Claude's awareness. At the end of the day, Ollama (also running locally) consolidates the conversation into structured memories for future sessions.

```
You ──► Claude Desktop
             │
             ▼  MCP (local only)
          Fewshot API
             │
       ┌─────┴──────┐
       │            │
   SQLite DB     Ollama
  (your machine) (your machine)
  preferences    nomic-embed-text
  memories       gemma4
  anti-patterns
```

Everything in that diagram runs on your hardware. The only outbound connection Fewshot makes is to Ollama's embedding and summarization models — which are also running locally.

---

## How it compares to Claude Skills

| | Claude Skills | Fewshot |
|---|---|---|
| **Data location** | Anthropic's servers | Your machine |
| **Privacy** | Subject to Anthropic's data policy | Fully private |
| **Content** | Static documents you upload | Living memory built from real work |
| **Context usage** | Included every message | Injected only when semantically relevant |
| **Learns over time** | No | Yes — reinforces patterns from actual usage |
| **Works across clients** | Claude.ai only | Any MCP-compatible client |
| **Cost** | Counts against context window | Selective injection, minimal overhead |

---

## Privacy scope — what Fewshot protects and what it doesn't

This is important to understand clearly.

### What Fewshot protects

**Context injection** — Everything Fewshot injects into Claude (memories, preferences, project facts) passes through a 3-stage audit pipeline before it reaches Claude. Connection strings, API keys, tokens, PII, and high-entropy secrets are redacted or blocked. Your past work never leaks back to Claude in raw form.

**MCP tool traffic** — With `Fewshot.Proxy` (see below), every argument passed to any MCP tool and every response returned by any MCP tool is scanned before forwarding. If a filesystem MCP server reads a file containing a database password and tries to return it, the proxy catches it before Claude sees it.

### What Fewshot does not protect

**Your direct conversation with Claude** — The messages you type and Claude's responses travel directly from Claude Desktop to `api.anthropic.com` over HTTPS. Fewshot has no visibility into this traffic. Your conversation is subject to [Anthropic's standard data policy](https://www.anthropic.com/privacy).

**Non-MCP integrations** — Browser-based Claude usage, the Claude API called directly by other tools, or any AI interaction that doesn't go through Claude Desktop's MCP layer is outside Fewshot's scope.

The practical implication: be mindful of what you paste directly into the Claude chat window. Fewshot protects what flows through MCP tools — it doesn't protect what you type.

---

## Fewshot.Proxy — MCP Audit Proxy

`Fewshot.Proxy` is an optional component that wraps any MCP server and scans all traffic before it reaches Claude or the MCP server. It's useful when you're running third-party MCP servers (filesystem, databases, browser automation) that may return sensitive data.

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

Blocking findings are redacted before forwarding. All findings are logged to the Fewshot API at `/api/proxy-audit/logs`.

### What it does not intercept

- The conversation itself (your messages and Claude's responses)
- HTTPS traffic to `api.anthropic.com`
- MCP servers not configured to run through the proxy

### Setup

Build the proxy:

```bash
dotnet build src/Fewshot.Proxy -c Release
```

Wrap each MCP server in `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "fewshot": {
      "command": "C:\\...\\Fewshot.Proxy.exe",
      "args": ["--server", "C:\\...\\Fewshot.Mcp.exe"]
    },
    "filesystem": {
      "command": "C:\\...\\Fewshot.Proxy.exe",
      "args": ["--server", "npx", "--", "@modelcontextprotocol/server-filesystem", "C:\\Users\\Joe"]
    }
  }
}
```

One proxy instance per MCP server. Each instance logs findings independently to Fewshot.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Ollama](https://ollama.com) — runs locally on Windows, Mac, Linux
- [Claude Desktop](https://claude.ai/download)

---

## Install

```powershell
irm https://raw.githubusercontent.com/fewshot-app/fewshot/feature/no-docker/install.ps1 | iex
```

The installer checks prerequisites, pulls Ollama models, installs as a Windows Service, and wires up Claude Desktop automatically.

---

## Manual setup

### 1. Pull Ollama models

```bash
ollama pull nomic-embed-text
ollama pull gemma4
```

### 2. Clone and build

```bash
git clone https://github.com/fewshot-app/fewshot.git
cd Fewshot
dotnet build
```

### 3. Start the API

```bash
dotnet run --project src/Fewshot.Api
```

Creates `%PROGRAMDATA%\Fewshot\fewshot.db` on first run. No migrations needed.

### 4. Configure Claude Desktop

Edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "fewshot": {
      "command": "C:\\path\\to\\Fewshot\\src\\Fewshot.Mcp\\bin\\Release\\net8.0\\Fewshot.Mcp.exe",
      "args": []
    }
  }
}
```

Restart Claude Desktop. Fewshot tools appear in the tools menu.

---

## Using Fewshot with Claude

Start every session by telling Claude what you're working on:

> *Call fewshot_get_context with hint "wordpress divi modules" before we start.*

Fewshot pulls relevant memories, preferences, and project facts into context. Work normally — Fewshot records the session in the background. At 2 AM the consolidation job turns it into structured memories for next time.

### MCP Tools

| Tool | Purpose |
|------|---------|
| `fewshot_get_context` | Pull relevant context at session start |
| `fewshot_record_message` | Record a message for nightly consolidation |
| `fewshot_search_memory` | Semantic search over past solutions |
| `fewshot_end_day` | Close session and trigger immediate consolidation |
| `fewshot_list_projects` | List configured projects |
| `fewshot_add_project` | Add a new project |
| `fewshot_remove_project` | Remove a project |

---

## Dashboard

```bash
dotnet run --project src/Fewshot.Dashboard
```

Open `http://localhost:5001` to browse memories, preferences, anti-patterns, sessions, projects, and proxy audit logs.

---

## Packs

Pre-built collections of memories, preferences, and anti-patterns for specific stacks — built from real production work. Enter a license key in the **Packs** page of the Dashboard. Fewshot downloads and imports everything automatically.

Available packs:
- WordPress / Divi Pro Pack
- .NET / Azure Pro Pack
- React / TypeScript Pro Pack
- Algolia Search Pack
- Full-Stack Bundle

Pack content is encrypted, licensed per machine, and decrypted locally. It never touches any cloud service on your end.

---

## How memory works

Every session recorded via `fewshot_record_message` is processed nightly by the consolidation job (Hangfire + Ollama gemma4). It extracts:

- **Memories** — specific solutions and outcomes, stored as 768-dim embeddings via nomic-embed-text
- **Preferences** — coding style and tooling preferences with confidence scores that strengthen each time the same pattern recurs
- **Anti-patterns** — things that explicitly failed
- **Suggestions** — actionable recommendations from past sessions

Semantic search uses cosine similarity computed in-process. No external vector database required.

---

## PII Protection pipeline

Fewshot runs a 3-stage audit on all context before injection:

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
dotnet publish src/Fewshot.Api -r win-x64 --self-contained -o publish/
sc create Fewshot binPath="C:\path\to\publish\Fewshot.Api.exe" start=auto
sc start Fewshot
```

---

## Project structure

```
src/
├── Fewshot.Core/           — Models, interfaces, enums
├── Fewshot.Infrastructure/ — EF Core (SQLite), services, memory, audit
├── Fewshot.Api/            — ASP.NET Core API, Hangfire, SignalR
├── Fewshot.Dashboard/      — Blazor WASM dashboard
├── Fewshot.Mcp/            — MCP stdio server for Claude Desktop
└── Fewshot.Proxy/          — MCP audit proxy (wraps any MCP server)
```

---

## License

MIT
