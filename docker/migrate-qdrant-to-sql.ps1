# migrate-qdrant-to-sql.ps1
# Scrolls all memories from Qdrant and migrates them into SQL Server 2025 via APEX API
# Run from: C:\Users\Joe\source\repos\APEX\docker

$QdrantBase  = "http://localhost:6333"
$QdrantKey   = "apex-dev-key"
$Collection  = "apex_memories"
$ApexBase    = "http://localhost:5000"

Write-Host "=== APEX Memory Migration: Qdrant -> SQL Server 2025 ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Scroll all points from Qdrant (with_vector=false — we re-embed from summary text)
Write-Host "Fetching all points from Qdrant..."
$scrollBody = '{"limit":100,"with_payload":true,"with_vector":false}'
$scrollResp = Invoke-RestMethod `
    -Uri "$QdrantBase/collections/$Collection/points/scroll" `
    -Method POST `
    -Headers @{ "api-key" = $QdrantKey } `
    -ContentType "application/json" `
    -Body $scrollBody

$points = $scrollResp.result.points
Write-Host "  Found $($points.Count) points in Qdrant." -ForegroundColor Green
Write-Host ""

if ($points.Count -eq 0) {
    Write-Host "Nothing to migrate. Exiting." -ForegroundColor Yellow
    exit 0
}

# Step 2: Create a dedicated migration session in APEX
Write-Host "Creating migration session..."
$sessionResp = Invoke-RestMethod `
    -Uri "$ApexBase/api/sessions" `
    -Method POST `
    -ContentType "application/json" `
    -Body "{}"
$sessionId = $sessionResp.sessionId
Write-Host "  Session ID: $sessionId" -ForegroundColor Green
Write-Host ""

# Step 3: Store each memory via POST /api/memory
$success = 0
$skipped = 0
$failed  = 0

foreach ($point in $points) {
    $p = $point.payload

    $summary = $p.summary
    if (-not $summary -or $summary.Length -lt 20) {
        Write-Host "  SKIP (no/short summary): $($point.id)" -ForegroundColor DarkYellow
        $skipped++
        continue
    }

    $body = [ordered]@{
        sessionId    = $sessionId
        summary      = $summary
        solution     = $p.solution
        approach     = $p.approach
        outcomeLabel = $p.outcome_label
        tags         = $p.tags
        language     = $p.language
        project      = $p.project
    } | ConvertTo-Json

    try {
        $result = Invoke-RestMethod `
            -Uri "$ApexBase/api/memory" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -ErrorAction Stop

        $preview = $summary.Substring(0, [Math]::Min(70, $summary.Length))
        Write-Host "  OK  [$($point.id.Substring(0,8))...] => [$($result.pointId.Substring(0,8))...] $preview" -ForegroundColor Green
        $success++
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 422) {
            $preview = $summary.Substring(0, [Math]::Min(60, $summary.Length))
            Write-Host "  SKIP (quality gate / duplicate): $preview" -ForegroundColor DarkYellow
            $skipped++
        } else {
            Write-Host "  FAIL [$($point.id)]: $($_.Exception.Message)" -ForegroundColor Red
            $failed++
        }
    }
}

Write-Host ""
Write-Host "=== Migration Complete ===" -ForegroundColor Cyan
Write-Host "  Migrated : $success" -ForegroundColor Green
Write-Host "  Skipped  : $skipped" -ForegroundColor Yellow
Write-Host "  Failed   : $failed" -ForegroundColor Red
