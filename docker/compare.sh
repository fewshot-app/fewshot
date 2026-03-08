#!/bin/bash
# APEX Format Comparison — See ACL vs Prose output side by side
# Run after seed.sh

API="http://localhost:5000/api"

echo "=== APEX Format Comparison ==="
echo ""

# Create a session
SESSION=$(curl -s -X POST "$API/sessions" | grep -o '"sessionId":[0-9]*' | grep -o '[0-9]*')
echo "Session: $SESSION"

# Write payload to temp file to avoid shell quoting issues
TMPFILE=$(mktemp)
cat > "$TMPFILE" <<ENDJSON
{
  "sessionId": $SESSION,
  "inputs": {
    "state": {
      "project": "WVU Medicine WordPress",
      "environment": "Staging",
      "branch": "feature/children-landing-refresh",
      "changedLast24h": [
        "wp-content/themes/flavor/children/header.php",
        "wp-content/plugins/wvum-algolia/includes/class-indexer.php",
        "wp-content/plugins/wvum-divi-modules/includes/FindADoc/FindADoc.php"
      ],
      "changedLast7d": [
        "wp-content/themes/flavor/functions.php",
        "wp-content/plugins/wvum-algolia/assets/js/search.js"
      ],
      "sprintItems": [
        {"description": "Logo/fonts/labels/icons quick wins", "priority": "High", "status": "InProgress", "count": 9},
        {"description": "Nav items, clear buttons, conditionals", "priority": "Medium", "status": "InProgress", "count": 7},
        {"description": "Page split, wait times, PBB page", "priority": "High", "status": "Blocked", "count": 6}
      ],
      "recentErrors": [
        {"description": "Algolia indexer timeout on bulk sync", "occurrenceCount": 3, "timeframe": "24h"},
        {"description": "Divi VB preview 500 on FindADoc module", "occurrenceCount": 1, "timeframe": "7d"}
      ],
      "lastDeployTime": "2026-02-20T14:30:00",
      "lastDeployStatus": "Success"
    },
    "memories": [
      {
        "summary": "Resolved Algolia cost spike by implementing SHA-256 hash-based change detection on the WP indexer. Only records with changed hashes get pushed to Algolia.",
        "solution": "Store record hashes in wp_postmeta, compare before push, skip unchanged records.",
        "outcomeLabel": "Worked",
        "relevanceScore": 0.91,
        "tags": "algolia,wordpress,cost-optimization,php",
        "createdAt": "2026-01-15T00:00:00",
        "sessionId": 1
      },
      {
        "summary": "Built custom Divi module pattern for WVU Medicine. ET_Builder_Module with PHP render() plus matching JSX component for Visual Builder.",
        "solution": "Extend ET_Builder_Module, implement get_fields() for props, render() for frontend, and create /includes/builder/ModuleName.jsx for VB.",
        "outcomeLabel": "Worked",
        "relevanceScore": 0.85,
        "tags": "divi,wordpress,php,jsx,visual-builder",
        "createdAt": "2026-01-20T00:00:00",
        "sessionId": 1
      },
      {
        "summary": "Debounced Algolia InstantSearch.js queries to reduce API operations. Was firing on every keystroke, now waits 300ms.",
        "solution": "Configure searchClient with custom requestOptions and 300ms debounce wrapper around searchFunction.",
        "outcomeLabel": "Worked",
        "relevanceScore": 0.78,
        "tags": "algolia,javascript,performance",
        "createdAt": "2026-02-01T00:00:00",
        "sessionId": 1
      }
    ],
    "antiPatterns": [
      {"antiPatternId":0,"sessionId":1,"pattern":"Algolia full-reindex on every WP save_post hook","reason":"Caused costs to spike from 300/mo to 5000/mo. Must use hash-based change detection.","language":"PHP","errorCode":"ALGOLIA_COST_SPIKE","createdAt":"2026-01-01T00:00:00"},
      {"antiPatternId":0,"sessionId":1,"pattern":"Synchronous Algolia search calls on page load","reason":"Must use InstantSearch.js with debounced client-side queries. Server-side sync calls added 800ms+ to TTFB.","language":"JavaScript","errorCode":"PERF_TTFB_HIGH","createdAt":"2026-01-01T00:00:00"},
      {"antiPatternId":0,"sessionId":1,"pattern":"Direct database queries from WordPress theme files","reason":"All data access must go through the REST API layer. Bypasses caching, auditing, and API versioning.","language":"PHP","errorCode":null,"createdAt":"2026-01-01T00:00:00"},
      {"antiPatternId":0,"sessionId":1,"pattern":"EF Core migrations in production with auto-migrate on startup","reason":"Failed during peak traffic. Use explicit SQL scripts during maintenance windows.","language":"C#","errorCode":"MIGRATION_TIMEOUT","createdAt":"2026-01-01T00:00:00"}
    ],
    "preferences": [
      {"prefId":0,"category":"CodingStyle","key":"PrimaryORM","value":"Dapper over EF for performance-critical queries. EF Core for standard CRUD.","confidenceScore":0.95,"reinforcementCount":8,"isExplicit":true,"lastUpdated":"2026-01-01T00:00:00"},
      {"prefId":0,"category":"CodingStyle","key":"AsyncPattern","value":"Async all the way. Never use .Result or .Wait(). Suffix async methods with Async.","confidenceScore":0.95,"reinforcementCount":10,"isExplicit":true,"lastUpdated":"2026-01-01T00:00:00"},
      {"prefId":0,"category":"Architecture","key":"ErrorHandling","value":"Prefer Result T pattern over exceptions for expected failures.","confidenceScore":0.9,"reinforcementCount":6,"isExplicit":true,"lastUpdated":"2026-01-01T00:00:00"},
      {"prefId":0,"category":"CodingStyle","key":"LINQStyle","value":"Fluent LINQ syntax over query syntax. Raw SQL for complex reporting queries.","confidenceScore":0.78,"reinforcementCount":6,"isExplicit":false,"lastUpdated":"2026-01-01T00:00:00"},
      {"prefId":0,"category":"CodingStyle","key":"CodeComments","value":"Minimal comments. Code should be self-documenting. XML docs on public interfaces only.","confidenceScore":0.72,"reinforcementCount":5,"isExplicit":false,"lastUpdated":"2026-01-01T00:00:00"}
    ],
    "facts": {
      "projects": [
        {"name":"wvumedicine.org","stack":".NET/WordPress hybrid, Divi theme, Algolia search","hostingInfo":"WP Engine"},
        {"name":"Find a Doc","stack":"Algolia InstantSearch.js, PHP REST API, WordPress","hostingInfo":"WP Engine"},
        {"name":"Connect Intranet","stack":"WordPress multisite, custom plugins","hostingInfo":"WP Engine"},
        {"name":"CrunchTime MCP","stack":".NET 8 Web API, SQL Server, Redis, Qdrant","hostingInfo":"Docker local"},
        {"name":"APEX","stack":".NET 8 Web API, SQL Server, Qdrant, Redis, Ollama","hostingInfo":"Docker local"}
      ],
      "endpoints": {
        "WVU Med Staging": "https://wvumedicinestg.wpenginepowered.com",
        "Find a Doc API": "https://wvumedicine.org/wp-json/wvum/v1/providers",
        "Algolia App": "https://dashboard.algolia.com/apps/WVUM_APP_ID"
      },
      "knownGoodPatterns": [
        "Hash-based change detection for Algolia indexing",
        "ET_Builder_Module + JSX pattern for Divi custom modules",
        "InstantSearch.js with 300ms debounce for search UX",
        "Hangfire with SQL Server job store for background processing"
      ],
      "pinnedVersions": {
        "dotnet": "8.0",
        "qdrant": "1.9.2",
        "redis": "7",
        "algolia-js": "4.x",
        "divi": "4.24"
      }
    }
  }
}
ENDJSON

# Call the build endpoint with file input
RESULT=$(curl -s -X POST "$API/context/build" \
  -H "Content-Type: application/json" \
  -d @"$TMPFILE")

rm -f "$TMPFILE"

echo ""
echo "$RESULT" | python3 -c "
import json, sys
data = json.load(sys.stdin)
print('ASSEMBLED CONTEXT:')
print('=' * 60)
print(data.get('assembledContext', 'ERROR: no context'))
print('=' * 60)
print()
segments = data.get('segments', [])
for seg in segments:
    tier = seg.get('tier', '?')
    fmt = seg.get('format', '?')
    used = seg.get('tokensUsed', 0)
    budget = seg.get('tokenBudget', 0)
    trunc = seg.get('wasTruncated', False)
    print(f'  {tier}: {fmt} | {used}/{budget} tokens | truncated={trunc}')
print()
print(f'Total tokens: {data.get(\"totalTokens\", 0)}')
print(f'Context hash: {data.get(\"contextHash\", \"?\")[:16]}...')
" 2>/dev/null || echo "$RESULT" | python3 -m json.tool 2>/dev/null || echo "$RESULT"

# Clean up session
curl -s -X POST "$API/sessions/$SESSION/end" > /dev/null

echo ""
echo "================================================================"
echo "  Run again to see different format assignments"
echo "  (experiments randomly assign ACL vs Prose per tier)"
echo "================================================================"
