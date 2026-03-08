#Requires -Version 5.1
<#
.SYNOPSIS
    APEX installer — Adaptive Personalized EXperience
.DESCRIPTION
    Downloads the latest APEX release, installs as a Windows Service,
    pulls Ollama models, and configures Claude Desktop MCP automatically.
.EXAMPLE
    irm https://raw.githubusercontent.com/jstarkwv/APEX/feature/no-docker/install.ps1 | iex
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Config ─────────────────────────────────────────────────────────────────────
$RepoOwner    = 'jstarkwv'
$RepoName     = 'APEX'
$ServiceName  = 'APEX'
$InstallDir   = "$env:LOCALAPPDATA\APEX"
$ApiPort      = 5000
$OllamaEmbed  = 'nomic-embed-text'
$OllamaChat   = 'qwen3:8b'

# ── Helpers ────────────────────────────────────────────────────────────────────
function Write-Step  { param($msg) Write-Host "`n  --> $msg" -ForegroundColor Cyan }
function Write-Ok    { param($msg) Write-Host "      [OK] $msg" -ForegroundColor Green }
function Write-Warn  { param($msg) Write-Host "      [WARN] $msg" -ForegroundColor Yellow }
function Write-Fail  { param($msg) Write-Host "`n  [FAIL] $msg" -ForegroundColor Red; exit 1 }

function Test-Admin {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $p  = [System.Security.Principal.WindowsPrincipal]$id
    return $p.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-DotnetVersion {
    try {
        $v = (dotnet --version 2>$null)
        if ($v -match '^(\d+)\.') { return [int]$Matches[1] }
    } catch {}
    return 0
}

function Get-OllamaRunning {
    try {
        $r = Invoke-WebRequest -Uri 'http://127.0.0.1:11434/api/tags' -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        return $r.StatusCode -eq 200
    } catch { return $false }
}

function Get-LatestReleaseAsset {
    param([string]$AssetPattern)
    $apiUrl = "https://api.github.com/repos/$RepoOwner/$RepoName/releases/latest"
    try {
        $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ 'User-Agent' = 'APEX-Installer' }
        $asset = $release.assets | Where-Object { $_.name -like $AssetPattern } | Select-Object -First 1
        if (-not $asset) { Write-Fail "No release asset matching '$AssetPattern' found. Have you created a GitHub Release?" }
        return $asset
    } catch {
        Write-Fail "Could not reach GitHub API: $_"
    }
}

# ── Banner ─────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════╗" -ForegroundColor DarkCyan
Write-Host "  ║   APEX — Adaptive Personalized EXperience  ║" -ForegroundColor DarkCyan
Write-Host "  ║           Installer v1.0                    ║" -ForegroundColor DarkCyan
Write-Host "  ╚══════════════════════════════════════════╝" -ForegroundColor DarkCyan
Write-Host ""

# ── Admin check ────────────────────────────────────────────────────────────────
Write-Step "Checking privileges"
if (-not (Test-Admin)) {
    Write-Warn "Not running as Administrator — will skip Windows Service registration."
    Write-Warn "Re-run as Administrator to install the service (recommended)."
    $script:SkipService = $true
} else {
    $script:SkipService = $false
    Write-Ok "Running as Administrator"
}

# ── .NET 8 check ───────────────────────────────────────────────────────────────
Write-Step "Checking .NET runtime"
$dotnetMajor = Get-DotnetVersion
if ($dotnetMajor -lt 8) {
    Write-Warn ".NET 8 SDK not found (detected: $dotnetMajor). Downloading installer..."
    $dotnetUrl = 'https://dot.net/v1/dotnet-install.ps1'
    $dotnetInstall = "$env:TEMP\dotnet-install.ps1"
    Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstall -UseBasicParsing
    & $dotnetInstall -Channel 8.0 -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet"
    $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
    Write-Ok ".NET 8 installed"
} else {
    Write-Ok ".NET $dotnetMajor found"
}

# ── Ollama check ───────────────────────────────────────────────────────────────
Write-Step "Checking Ollama"
$ollamaExe = Get-Command ollama -ErrorAction SilentlyContinue
if (-not $ollamaExe) {
    Write-Host ""
    Write-Host "  Ollama is not installed. Please install it from https://ollama.com" -ForegroundColor Yellow
    Write-Host "  After installing, re-run this script." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "  Open ollama.com in your browser now? [Y/n]"
    if ($response -ne 'n' -and $response -ne 'N') {
        Start-Process 'https://ollama.com'
    }
    exit 1
} else {
    Write-Ok "Ollama found at $($ollamaExe.Source)"
}

if (-not (Get-OllamaRunning)) {
    Write-Warn "Ollama is not running. Starting it..."
    Start-Process ollama -ArgumentList 'serve' -WindowStyle Hidden
    Start-Sleep -Seconds 3
    if (-not (Get-OllamaRunning)) {
        Write-Fail "Could not start Ollama. Please run 'ollama serve' manually and re-run the installer."
    }
    Write-Ok "Ollama started"
} else {
    Write-Ok "Ollama is running"
}

# ── Pull models ────────────────────────────────────────────────────────────────
Write-Step "Pulling Ollama models (this may take a few minutes on first run)"

Write-Host "      Pulling $OllamaEmbed..." -ForegroundColor Gray
ollama pull $OllamaEmbed
Write-Ok "$OllamaEmbed ready"

Write-Host "      Pulling $OllamaChat..." -ForegroundColor Gray
ollama pull $OllamaChat
Write-Ok "$OllamaChat ready"

# ── Download release ───────────────────────────────────────────────────────────
Write-Step "Downloading latest APEX release"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallDir\mcp" | Out-Null

# API
$apiAsset = Get-LatestReleaseAsset 'Apex.Api-win-x64.zip'
Write-Host "      Downloading $($apiAsset.name) ($([math]::Round($apiAsset.size/1MB, 1)) MB)..." -ForegroundColor Gray
$apiZip = "$env:TEMP\Apex.Api.zip"
Invoke-WebRequest -Uri $apiAsset.browser_download_url -OutFile $apiZip -UseBasicParsing
Expand-Archive -Path $apiZip -DestinationPath "$InstallDir\api" -Force
Remove-Item $apiZip
Write-Ok "API extracted to $InstallDir\api"

# MCP
$mcpAsset = Get-LatestReleaseAsset 'Apex.Mcp-win-x64.zip'
Write-Host "      Downloading $($mcpAsset.name) ($([math]::Round($mcpAsset.size/1MB, 1)) MB)..." -ForegroundColor Gray
$mcpZip = "$env:TEMP\Apex.Mcp.zip"
Invoke-WebRequest -Uri $mcpAsset.browser_download_url -OutFile $mcpZip -UseBasicParsing
Expand-Archive -Path $mcpZip -DestinationPath "$InstallDir\mcp" -Force
Remove-Item $mcpZip
Write-Ok "MCP server extracted to $InstallDir\mcp"

# Dashboard
$dashAsset = Get-LatestReleaseAsset 'Apex.Dashboard-win-x64.zip'
if ($dashAsset) {
    Write-Host "      Downloading $($dashAsset.name)..." -ForegroundColor Gray
    $dashZip = "$env:TEMP\Apex.Dashboard.zip"
    Invoke-WebRequest -Uri $dashAsset.browser_download_url -OutFile $dashZip -UseBasicParsing
    Expand-Archive -Path $dashZip -DestinationPath "$InstallDir\dashboard" -Force
    Remove-Item $dashZip
    Write-Ok "Dashboard extracted to $InstallDir\dashboard"
}

# ── Windows Service ────────────────────────────────────────────────────────────
$apiExe = "$InstallDir\api\Apex.Api.exe"

if (-not $script:SkipService) {
    Write-Step "Registering Windows Service"

    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "      Stopping existing service..." -ForegroundColor Gray
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    sc.exe create $ServiceName `
        binPath= "`"$apiExe`"" `
        start= auto `
        DisplayName= "APEX AI Middleware" | Out-Null

    sc.exe description $ServiceName "APEX local AI middleware — personalized context for Claude Desktop" | Out-Null
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 3

    $svc = Get-Service -Name $ServiceName
    if ($svc.Status -eq 'Running') {
        Write-Ok "Service '$ServiceName' is running"
    } else {
        Write-Warn "Service registered but not running. Check Event Viewer for errors."
    }
} else {
    Write-Step "Skipping service registration (not admin)"
    Write-Host "      To start APEX manually: $apiExe" -ForegroundColor Gray
}

# ── Wait for API ready ─────────────────────────────────────────────────────────
Write-Step "Waiting for APEX API"
$retries = 15
$ready = $false
for ($i = 0; $i -lt $retries; $i++) {
    try {
        $health = Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/health" -TimeoutSec 2 -ErrorAction Stop
        if ($health.status -in 'healthy','degraded') { $ready = $true; break }
    } catch {}
    Write-Host "      Waiting... ($($i+1)/$retries)" -ForegroundColor Gray
    Start-Sleep -Seconds 2
}
if (-not $ready) {
    Write-Warn "API did not respond at http://127.0.0.1:$ApiPort/health — check logs after install."
} else {
    Write-Ok "API is responding (status: $($health.status))"
}

# ── Seed default projects ──────────────────────────────────────────────────────
if ($ready) {
    Write-Step "Seeding default projects"

    $defaultProjects = @(
        @{ name='general';   displayName='General';          keywords='general, misc, scratch';            facts=$null },
        @{ name='apex';      displayName='APEX';             keywords='apex, mcp, middleware, ai, ollama'; facts='Local AI middleware project. Stack: .NET 8, SQLite, Ollama.' },
        @{ name='wordpress'; displayName='WordPress / Divi'; keywords='wordpress, divi, php, wp, plugin';  facts='WVU Medicine WordPress/Divi custom modules plugin.' },
        @{ name='dotnet';    displayName='.NET / C#';        keywords='dotnet, csharp, c#, asp.net, ef';   facts='.NET 8 projects including APIs and background services.' },
        @{ name='react';     displayName='React / JS';       keywords='react, javascript, js, ts, vite';   facts='Frontend React and vanilla JS projects.' },
        @{ name='devops';    displayName='DevOps';           keywords='devops, ci, cd, azure, github, pipeline'; facts='CI/CD pipelines, Azure DevOps, GitHub Actions.' }
    )

    $headers = @{ 'Content-Type' = 'application/json' }
    foreach ($proj in $defaultProjects) {
        try {
            $body = $proj | ConvertTo-Json
            Invoke-RestMethod -Uri "http://127.0.0.1:$ApiPort/api/projects" `
                -Method Post -Headers $headers -Body $body -ErrorAction Stop | Out-Null
            Write-Ok "Project: $($proj.displayName)"
        } catch {
            $msg = $_.ToString()
            if ($msg -like '*409*' -or $msg -like '*already*' -or $msg -like '*Conflict*') {
                Write-Host "      [SKIP] $($proj.displayName) already exists" -ForegroundColor Gray
            } else {
                Write-Warn "Could not seed project '$($proj.name)': $msg"
            }
        }
    }
}

# ── Claude Desktop MCP config ──────────────────────────────────────────────────
Write-Step "Configuring Claude Desktop MCP"

$mcpExe = "$InstallDir\mcp\Apex.Mcp.exe"
$claudeConfigDir  = "$env:APPDATA\Claude"
$claudeConfigFile = "$claudeConfigDir\claude_desktop_config.json"

New-Item -ItemType Directory -Force -Path $claudeConfigDir | Out-Null

if (Test-Path $claudeConfigFile) {
    # Merge into existing config
    $existing = Get-Content $claudeConfigFile -Raw | ConvertFrom-Json
    if (-not $existing.mcpServers) {
        $existing | Add-Member -MemberType NoteProperty -Name mcpServers -Value @{}
    }
    $existing.mcpServers | Add-Member -MemberType NoteProperty -Name apex -Value @{
        command = $mcpExe
        args    = @()
    } -Force
    $existing | ConvertTo-Json -Depth 10 | Set-Content $claudeConfigFile -Encoding UTF8
    Write-Ok "Merged apex MCP entry into existing claude_desktop_config.json"
} else {
    # Create fresh config
    @{
        mcpServers = @{
            apex = @{
                command = $mcpExe
                args    = @()
            }
        }
    } | ConvertTo-Json -Depth 5 | Set-Content $claudeConfigFile -Encoding UTF8
    Write-Ok "Created claude_desktop_config.json"
}

# ── Add to PATH ────────────────────────────────────────────────────────────────
Write-Step "Updating PATH"
$currentPath = [System.Environment]::GetEnvironmentVariable('PATH', 'User')
if ($currentPath -notlike "*$InstallDir\api*") {
    [System.Environment]::SetEnvironmentVariable('PATH', "$currentPath;$InstallDir\api", 'User')
    Write-Ok "Added $InstallDir\api to user PATH"
} else {
    Write-Ok "PATH already contains APEX api directory"
}

# ── Summary ────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  ══════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host "   APEX installation complete!" -ForegroundColor Green
Write-Host "  ══════════════════════════════════════════════" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "   API:        http://localhost:$ApiPort" -ForegroundColor White
Write-Host "   Swagger:    http://localhost:$ApiPort/swagger" -ForegroundColor White
Write-Host "   Hangfire:   http://localhost:$ApiPort/hangfire" -ForegroundColor White
if (Test-Path "$InstallDir\dashboard") {
    Write-Host "   Dashboard:  run 'Apex.Dashboard.exe' in $InstallDir\dashboard" -ForegroundColor White
}
Write-Host ""
Write-Host "   Next steps:" -ForegroundColor Yellow
Write-Host "   1. Restart Claude Desktop to activate the MCP tools" -ForegroundColor White
Write-Host "   2. In Claude, start a session with:" -ForegroundColor White
Write-Host "      'Call apex_get_context with hint <what you're working on>'" -ForegroundColor Gray
Write-Host ""
if ($script:SkipService) {
    Write-Host "   To run APEX as a service, re-run this script as Administrator." -ForegroundColor Yellow
    Write-Host ""
}
Write-Host "   To uninstall: irm https://raw.githubusercontent.com/$RepoOwner/$RepoName/feature/no-docker/uninstall.ps1 | iex" -ForegroundColor Gray
Write-Host ""
