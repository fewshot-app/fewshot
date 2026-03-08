#!/bin/bash
# APEX Seed Data — Realistic test data from Joe's actual projects
# Run after init.sh, with API running at localhost:5000

set -e
API="http://localhost:5000/api"

echo "=== APEX Seed Data ==="
echo ""

# ── Create a session for FK references ────────────────────────────
echo "Creating seed session..."
SESSION=$(curl -s -X POST "$API/sessions" | grep -o '"sessionId":[0-9]*' | grep -o '[0-9]*')
echo "  Session ID: $SESSION"

# ── Log some messages so we have MessageIds for suggestions ───────
echo "Seeding messages..."
MSG1=$(curl -s -X POST "$API/messages" \
  -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"role\":\"User\",\"content\":\"Can you help me optimize the Algolia indexing? We're getting hit with huge API costs.\",\"tokenCount\":25}" \
  | grep -o '"messageId":[0-9]*' | grep -o '[0-9]*')

MSG2=$(curl -s -X POST "$API/messages" \
  -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"role\":\"Assistant\",\"content\":\"The issue is that the indexer is re-uploading every record on every sync. We should implement hash-based change detection using SHA-256 of the serialized record, then only push records whose hash differs from the stored hash.\",\"tokenCount\":48}" \
  | grep -o '"messageId":[0-9]*' | grep -o '[0-9]*')

MSG3=$(curl -s -X POST "$API/messages" \
  -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"role\":\"User\",\"content\":\"The Divi module for the provider search isn't rendering correctly in the Visual Builder.\",\"tokenCount\":20}" \
  | grep -o '"messageId":[0-9]*' | grep -o '[0-9]*')

MSG4=$(curl -s -X POST "$API/messages" \
  -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"role\":\"Assistant\",\"content\":\"The Visual Builder requires a separate JSX component that mirrors the PHP render. You need to extend ET_Builder_Module and implement both the render() method and the corresponding React component.\",\"tokenCount\":42}" \
  | grep -o '"messageId":[0-9]*' | grep -o '[0-9]*')

echo "  Messages: $MSG1, $MSG2, $MSG3, $MSG4"

# ── Preferences (explicit + inferred) ─────────────────────────────
echo "Seeding preferences..."

# Explicit preferences
curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"CodingStyle","key":"PrimaryORM","value":"Dapper over EF for performance-critical queries. EF Core for standard CRUD.","confidenceScore":0.95,"isExplicit":true}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"CodingStyle","key":"NamingConvention","value":"PascalCase for public members, camelCase for private, _prefix for private fields","confidenceScore":0.99,"isExplicit":true}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"Architecture","key":"ErrorHandling","value":"Prefer Result<T> pattern over exceptions for expected failures. Exceptions for truly exceptional cases only.","confidenceScore":0.9,"isExplicit":true}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"Tooling","key":"ContainerOrchestration","value":"Docker Compose for local dev, no Kubernetes","confidenceScore":0.99,"isExplicit":true}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"CodingStyle","key":"AsyncPattern","value":"Async all the way. Never use .Result or .Wait(). Suffix async methods with Async.","confidenceScore":0.95,"isExplicit":true}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"Architecture","key":"APIStyle","value":"Minimal APIs for simple endpoints, Controllers for complex ones with DI","confidenceScore":0.85,"isExplicit":true}' > /dev/null

# Inferred preferences
curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"CodingStyle","key":"CodeComments","value":"Minimal comments. Code should be self-documenting. XML docs on public interfaces only.","confidenceScore":0.72,"reinforcementCount":5,"isExplicit":false}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"Tooling","key":"TestFramework","value":"xUnit with FluentAssertions. Moq for mocking.","confidenceScore":0.65,"reinforcementCount":3,"isExplicit":false}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"Architecture","key":"ConfigPattern","value":"IOptions<T> with strongly-typed settings classes. No magic strings for config keys.","confidenceScore":0.68,"reinforcementCount":4,"isExplicit":false}' > /dev/null

curl -s -X POST "$API/preferences" -H "Content-Type: application/json" \
  -d '{"category":"CodingStyle","key":"LINQStyle","value":"Fluent LINQ syntax over query syntax. Raw SQL for complex reporting queries.","confidenceScore":0.78,"reinforcementCount":6,"isExplicit":false}' > /dev/null

echo "  10 preferences seeded."

# ── Anti-patterns ─────────────────────────────────────────────────
echo "Seeding anti-patterns..."

curl -s -X POST "$API/antipatterns" -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"pattern\":\"Algolia full-reindex on every WP save_post hook\",\"reason\":\"Caused API costs to spike from \$300/mo to \$5000/mo. Must use hash-based change detection to only push modified records.\",\"language\":\"PHP\",\"errorCode\":\"ALGOLIA_COST_SPIKE\"}" > /dev/null

curl -s -X POST "$API/antipatterns" -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"pattern\":\"Newtonsoft.Json with default settings in .NET 8 APIs\",\"reason\":\"System.Text.Json is the default in .NET 8. Newtonsoft adds unnecessary dependency and has slower serialization. Only use Newtonsoft if you need specific features like JObject manipulation.\",\"language\":\"C#\"}" > /dev/null

curl -s -X POST "$API/antipatterns" -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"pattern\":\"Direct database queries from WordPress theme files\",\"reason\":\"All data access must go through the REST API layer. Direct DB access bypasses caching, auditing, and the API versioning strategy.\",\"language\":\"PHP\"}" > /dev/null

curl -s -X POST "$API/antipatterns" -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"pattern\":\"Using .env files for secrets in Docker Compose production\",\"reason\":\"Use dotnet user-secrets for dev and Azure Key Vault or Docker secrets for production. .env files get committed to source control.\",\"language\":null}" > /dev/null

curl -s -X POST "$API/antipatterns" -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"pattern\":\"Synchronous Algolia search calls on page load\",\"reason\":\"Must use InstantSearch.js with debounced client-side queries. Server-side sync calls added 800ms+ to TTFB on Find a Doc pages.\",\"language\":\"JavaScript\",\"errorCode\":\"PERF_TTFB_HIGH\"}" > /dev/null

curl -s -X POST "$API/antipatterns" -H "Content-Type: application/json" \
  -d "{\"sessionId\":$SESSION,\"pattern\":\"EF Core migrations in production with auto-migrate on startup\",\"reason\":\"Failed during peak traffic. Always use explicit SQL scripts applied during maintenance windows. EF migrations for dev only.\",\"language\":\"C#\",\"errorCode\":\"MIGRATION_TIMEOUT\"}" > /dev/null

echo "  6 anti-patterns seeded."

# ── Suggestions + Outcomes ────────────────────────────────────────
echo "Seeding suggestions and outcomes..."

# Suggestion 1: Algolia hash optimization (Worked)
SUG1=$(curl -s -X POST "$API/suggestions" -H "Content-Type: application/json" \
  -d "{\"messageId\":$MSG2,\"suggestionType\":\"CodeSnippet\",\"content\":\"Implement SHA-256 hash comparison on serialized Algolia records before pushing. Store hashes in wp_postmeta. Only push when hash differs.\",\"language\":\"PHP\",\"filePath\":\"wp-content/plugins/wvum-algolia/includes/class-indexer.php\",\"extractionMethod\":\"Manual\",\"extractionConfidence\":0.95}" \
  | grep -o '"suggestionId":[0-9]*' | grep -o '[0-9]*')

curl -s -X POST "$API/outcomes" -H "Content-Type: application/json" \
  -d "{\"suggestionId\":$SUG1,\"status\":\"Worked\",\"notes\":\"Reduced Algolia API operations by 94%. Monthly cost dropped from \$5200 to \$280.\",\"effortSavedMinutes\":480,\"confirmedByGit\":true,\"isExplicit\":true}" > /dev/null

# Suggestion 2: Divi module pattern (Worked)
SUG2=$(curl -s -X POST "$API/suggestions" -H "Content-Type: application/json" \
  -d "{\"messageId\":$MSG4,\"suggestionType\":\"ArchitecturalPattern\",\"content\":\"Extend ET_Builder_Module with render() for frontend and create matching JSX component in /includes/builder/ for Visual Builder preview.\",\"language\":\"PHP\",\"filePath\":\"wp-content/plugins/wvum-divi-modules/\",\"extractionMethod\":\"Manual\",\"extractionConfidence\":0.9}" \
  | grep -o '"suggestionId":[0-9]*' | grep -o '[0-9]*')

curl -s -X POST "$API/outcomes" -H "Content-Type: application/json" \
  -d "{\"suggestionId\":$SUG2,\"status\":\"Worked\",\"notes\":\"Pattern now used for all custom Divi modules. VB preview matches frontend rendering.\",\"effortSavedMinutes\":120,\"confirmedByGit\":true,\"isExplicit\":true}" > /dev/null

# Suggestion 3: A failed suggestion
SUG3=$(curl -s -X POST "$API/suggestions" -H "Content-Type: application/json" \
  -d "{\"messageId\":$MSG2,\"suggestionType\":\"ConfigChange\",\"content\":\"Use Algolia's built-in incremental sync with their WordPress plugin.\",\"language\":\"PHP\",\"extractionMethod\":\"Regex\",\"extractionConfidence\":0.6}" \
  | grep -o '"suggestionId":[0-9]*' | grep -o '[0-9]*')

curl -s -X POST "$API/outcomes" -H "Content-Type: application/json" \
  -d "{\"suggestionId\":$SUG3,\"status\":\"Failed\",\"notes\":\"Algolia WP plugin does not support custom post types with ACF fields. Had to build custom indexer.\",\"errorCode\":\"ALGOLIA_PLUGIN_LIMITATION\",\"isExplicit\":true}" > /dev/null

echo "  3 suggestions + 3 outcomes seeded."

# ── End the seed session ──────────────────────────────────────────
curl -s -X POST "$API/sessions/$SESSION/end" > /dev/null

# ── Create an experiment ──────────────────────────────────────────
echo "Creating ACL vs Prose experiments..."

curl -s -X POST "$API/experiments" -H "Content-Type: application/json" \
  -d '{"name":"P1 Current State: ACL vs Prose","tier":"P1","targetSessions":60}' > /dev/null

curl -s -X POST "$API/experiments" -H "Content-Type: application/json" \
  -d '{"name":"P2 Semantic Memory: ACL vs Prose","tier":"P2","targetSessions":60}' > /dev/null

curl -s -X POST "$API/experiments" -H "Content-Type: application/json" \
  -d '{"name":"P3 Anti-Patterns: ACL vs Prose","tier":"P3","targetSessions":60}' > /dev/null

curl -s -X POST "$API/experiments" -H "Content-Type: application/json" \
  -d '{"name":"P4 Preferences: ACL vs Prose","tier":"P4","targetSessions":60}' > /dev/null

curl -s -X POST "$API/experiments" -H "Content-Type: application/json" \
  -d '{"name":"P5 Project Facts: ACL vs Prose","tier":"P5","targetSessions":60}' > /dev/null

echo "  5 experiments created (P1-P5)."

# ── Summary ───────────────────────────────────────────────────────
echo ""
echo "=== Seed Complete ==="
echo ""
echo "Test context building:"
echo "  curl -s http://localhost:5000/api/preferences | python -m json.tool"
echo "  curl -s http://localhost:5000/api/antipatterns | python -m json.tool"
echo ""
echo "Swagger UI: http://localhost:5000/swagger"
