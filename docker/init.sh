#!/bin/bash
# APEX Post-Deploy Initialization
# Run once after: docker compose up -d
# Run from the /docker directory

set -e

SA_PASSWORD="${SA_PASSWORD:-Apex_Dev_2026!}"

echo "=== APEX Infrastructure Init ==="
echo ""

# ── Wait for SQL Server ────────────────────────────────────────────
echo "Waiting for SQL Server..."
for i in $(seq 1 30); do
  if docker exec apex-sql-server /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1" -C -b -l 5 > /dev/null 2>&1; then
    echo "  SQL Server ready."
    break
  fi
  if [ "$i" -eq 30 ]; then echo "  ERROR: SQL Server not ready after 30 attempts."; exit 1; fi
  sleep 2
done

# ── Create database ───────────────────────────────────────────────
echo "Creating ApexDb database..."
docker exec apex-sql-server /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -C \
  -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ApexDb') CREATE DATABASE ApexDb"
echo "  Done."

# ── Run schema migrations ────────────────────────────────────────
for migration in 001_InitialSchema 002_SystemSettings 003_SessionProject 004_Projects 005_Memories; do
  echo "Running migration (${migration}.sql)..."
  docker cp ../src/Apex.Infrastructure/Data/Migrations/${migration}.sql apex-sql-server:/tmp/${migration}.sql
  docker exec apex-sql-server /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -d ApexDb -C \
    -i /tmp/${migration}.sql
  echo "  Done."
done

# ── Verify tables ─────────────────────────────────────────────────
echo "Verifying tables..."
TABLE_COUNT=$(docker exec apex-sql-server /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -d ApexDb -C -h -1 \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
TABLE_COUNT=$(echo "$TABLE_COUNT" | tr -d '[:space:]')
echo "  Found $TABLE_COUNT tables."

# ── Pull Ollama models ────────────────────────────────────────────
echo "Pulling Ollama models (this may take a few minutes)..."
echo "  Pulling nomic-embed-text..."
docker exec apex-ollama ollama pull nomic-embed-text
echo "  Pulling qwen3:8b..."
docker exec apex-ollama ollama pull qwen3:8b
echo "  Done."

# ── Verify Redis ──────────────────────────────────────────────────
echo "Verifying Redis..."
REDIS_PONG=$(docker exec apex-redis redis-cli -a "${REDIS_PASSWORD:-apex-dev-redis}" ping 2>/dev/null)
if [ "$REDIS_PONG" = "PONG" ]; then
  echo "  Redis ready."
else
  echo "  WARNING: Redis did not respond."
fi

# ── Summary ───────────────────────────────────────────────────────
echo ""
echo "=== APEX Init Complete ==="
echo ""
docker compose ps
echo ""
echo "API will be available at: http://localhost:5000"
echo "Swagger UI:               http://localhost:5000/swagger"
echo "Health check:             http://localhost:5000/health"
