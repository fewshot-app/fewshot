#Requires -Version 5.1
<#
.SYNOPSIS
    Fewshot installer -- Context packs & memory for AI agents
.DESCRIPTION
    Downloads the latest Fewshot release, installs Ollama (if needed),
    installs as a Windows Service, pulls models, and configures Claude Desktop MCP.
.EXAMPLE
    irm https://raw.githubusercontent.com/fewshot-app/fewshot/main/install.ps1 | iex
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Config ─────────────────────────────────────────────────────────────────────
$RepoOwner    = 'fewshot-app'
$RepoName     = 'fewshot'
$ServiceName  = 'Fewshot'
$InstallDir   = "$env:LOCALAPPDATA\Fewshot"
$ApiPort      = 5000
$OllamaEmbed  = 'nomic-embed-text'
$OllamaChat   = 'gemma4'

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
        $release = Invoke-RestMethod -Uri $apiUrl -Headers @{ 'User-Agent' = 'Fewshot-Installer' }
        $asset = $release.assets | Where-Object { $_.name -like $AssetPattern } | Select-Object -First 1
        if (-not $asset) { Write-Fail "No release asset matching '$AssetPattern' found. Have you created a GitHub Release?" }
        return $asset
    } catch {
        Write-Fail "Could not reach GitHub API: $_"
    }
}

function Invoke-OllamaPull {
    param([string]$Model)
    # Ollama outputs ANSI progress spinners to stderr which PowerShell treats as errors.
    # Use Start-Process to isolate it completely from PowerShell's error stream.
    $proc = Start-Process -FilePath 'ollama' -ArgumentList "pull $Model" -NoNewWindow -Wait -PassThru
    return $proc.ExitCode -eq 0
}

# ── Banner ─────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  +==========================================+" -ForegroundColor DarkCyan
Write-Host "  |   Fewshot -- Context packs & memory for AI agents |" -ForegroundColor DarkCyan
Write-Host "  |           Installer v1.4                    |" -ForegroundColor DarkCyan
Write-Host "  +==========================================+" -ForegroundColor DarkCyan
Write-Host ""

# ── Admin check ────────────────────────────────────────────────────────────────
Write-Step "Checking privileges"
if (-not (Test-Admin)) {
    Write-Warn "Not running as Administrator -- will skip Windows Service registration."
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

# ── Ollama install + start ─────────────────────────────────────────────────────
Write-Step "Checking Ollama"
$ollamaExe = Get-Command ollama -ErrorAction SilentlyContinue
if (-not $ollamaExe) {
    Write-Host "      Ollama not found. Installing..." -ForegroundColor Yellow

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "      Installing via winget (silent)..." -ForegroundColor Gray
        winget install --id Ollama.Ollama --accept-package-agreements --accept-source-agreements --silent
    } else {
        Write-Host "      winget not available. Downloading OllamaSetup.exe..." -ForegroundColor Gray
        $ollamaInstaller = "$env:TEMP\OllamaSetup.exe"
        Invoke-WebRequest -Uri 'https://ollama.com/download/OllamaSetup.exe' -OutFile $ollamaInstaller -UseBasicParsing
        Write-Host "      Running installer -- please complete the setup wizard if it appears..." -ForegroundColor Yellow
        $proc = Start-Process -FilePath $ollamaInstaller -Wait -PassThru
        Remove-Item $ollamaInstaller -Force -ErrorAction SilentlyContinue
        if ($proc.ExitCode -ne 0 -and $proc.ExitCode -ne $null) {
            Write-Warn "Ollama installer exited with code $($proc.ExitCode)"
        }
    }

    $machinePath = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine')
    $userPath    = [System.Environment]::GetEnvironmentVariable('PATH', 'User')
    $env:PATH    = "$machinePath;$userPath"

    $ollamaExe = Get-Command ollama -ErrorAction SilentlyContinue
    if (-not $ollamaExe) {
        $defaultPath = "$env:LOCALAPPDATA\Programs\Ollama"
        if (Test-Path "$defaultPath\ollama.exe") {
            $env:PATH = "$defaultPath;$env:PATH"
            $ollamaExe = Get-Command ollama -ErrorAction SilentlyContinue
        }
    }

    if (-not $ollamaExe) {
        Write-Fail "Ollama installed but not found in PATH. Close and reopen PowerShell, then re-run the installer."
    }

    Write-Ok "Ollama installed at $($ollamaExe.Source)"
} else {
    Write-Ok "Ollama found at $($ollamaExe.Source)"
}

if (-not (Get-OllamaRunning)) {
    Write-Host "      Starting Ollama..." -ForegroundColor Gray
    Start-Process ollama -ArgumentList 'serve' -WindowStyle Hidden
    $ollamaReady = $false
    for ($i = 0; $i -lt 15; $i++) {
        Start-Sleep -Seconds 1
        if (Get-OllamaRunning) { $ollamaReady = $true; break }
    }
    if (-not $ollamaReady) {
        Write-Fail "Could not start Ollama after 15 seconds. Check if port 11434 is in use."
    }
    Write-Ok "Ollama started"
} else {
    Write-Ok "Ollama is running"
}

# ── Pull models ────────────────────────────────────────────────────────────────
Write-Step "Pulling Ollama models (this may take a few minutes on first run)"

Write-Host "      Pulling $OllamaEmbed..." -ForegroundColor Gray
if (Invoke-OllamaPull $OllamaEmbed) {
    Write-Ok "$OllamaEmbed ready"
} else {
    Write-Warn "Failed to pull $OllamaEmbed -- you can pull it manually later: ollama pull $OllamaEmbed"
}

Write-Host "      Pulling $OllamaChat..." -ForegroundColor Gray
if (Invoke-OllamaPull $OllamaChat) {
    Write-Ok "$OllamaChat ready"
} else {
    Write-Warn "Failed to pull $OllamaChat -- you can pull it manually later: ollama pull $OllamaChat"
}

# ── Component selection ────────────────────────────────────────────────────────
Write-Step "Choose optional components"
Write-Host ""
Write-Host "      [Required] API + MCP Server (core)" -ForegroundColor White
Write-Host ""

$installDashboard = Read-Host "      Install Dashboard -- web UI for memories/sessions? [Y/n]"
$installDashboard = $installDashboard -ne 'n' -and $installDashboard -ne 'N'

Write-Host ""
$components = @("API", "MCP")
if ($installDashboard) { $components += "Dashboard" }
Write-Ok "Components: $($components -join ', ')"

# ── Stop running Fewshot processes ────────────────────────────────────────────────
Write-Step "Stopping running Fewshot processes (if any)"

# Stop Windows Service if running
$existingSvc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingSvc -and $existingSvc.Status -ne 'Stopped') {
    Write-Host "      Stopping Fewshot service..." -ForegroundColor Gray
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    for ($i = 0; $i -lt 30; $i++) {
        $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if (-not $svc -or $svc.Status -eq 'Stopped') { break }
        Start-Sleep -Seconds 1
    }
    Write-Ok "Fewshot service stopped"
} else {
    Write-Host "      Fewshot service not running" -ForegroundColor Gray
}

# Kill any standalone Fewshot.Api.exe (non-service runs)
Get-Process -Name 'Fewshot.Api' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "      Killing Fewshot.Api.exe (PID $($_.Id))..." -ForegroundColor Gray
    $_ | Stop-Process -Force
}

# Kill Fewshot.Mcp.exe (Claude Desktop spawns this)
Get-Process -Name 'Fewshot.Mcp' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "      Killing Fewshot.Mcp.exe (PID $($_.Id))..." -ForegroundColor Gray
    $_ | Stop-Process -Force
}

# Brief pause for file handles to release
Start-Sleep -Seconds 2
Write-Ok "Ready to update"

# ── Download release ───────────────────────────────────────────────────────────
Write-Step "Downloading latest Fewshot release"

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path "$InstallDir\mcp" | Out-Null

# API
$apiAsset = Get-LatestReleaseAsset 'Fewshot.Api-win-x64.zip'
Write-Host "      Downloading $($apiAsset.name) ($([math]::Round($apiAsset.size/1MB, 1)) MB)..." -ForegroundColor Gray
$apiZip = "$env:TEMP\Fewshot.Api.zip"
Invoke-WebRequest -Uri $apiAsset.browser_download_url -OutFile $apiZip -UseBasicParsing
Expand-Archive -Path $apiZip -DestinationPath "$InstallDir\api" -Force
Remove-Item $apiZip
Write-Ok "API extracted to $InstallDir\api"

# MCP
$mcpAsset = Get-LatestReleaseAsset 'Fewshot.Mcp-win-x64.zip'
Write-Host "      Downloading $($mcpAsset.name) ($([math]::Round($mcpAsset.size/1MB, 1)) MB)..." -ForegroundColor Gray
$mcpZip = "$env:TEMP\Fewshot.Mcp.zip"
Invoke-WebRequest -Uri $mcpAsset.browser_download_url -OutFile $mcpZip -UseBasicParsing
Expand-Archive -Path $mcpZip -DestinationPath "$InstallDir\mcp" -Force
Remove-Item $mcpZip
Write-Ok "MCP server extracted to $InstallDir\mcp"

# Dashboard
if ($installDashboard) {
    $dashAsset = Get-LatestReleaseAsset 'Fewshot.Dashboard*.zip'
    if ($dashAsset) {
        Write-Host "      Downloading $($dashAsset.name)..." -ForegroundColor Gray
        $dashZip = "$env:TEMP\Fewshot.Dashboard.zip"
        Invoke-WebRequest -Uri $dashAsset.browser_download_url -OutFile $dashZip -UseBasicParsing
        Expand-Archive -Path $dashZip -DestinationPath "$InstallDir\dashboard" -Force
        Remove-Item $dashZip
        Write-Ok "Dashboard extracted to $InstallDir\dashboard"
    }
}

# ── Windows Service ────────────────────────────────────────────────────────────
$apiExe = "$InstallDir\api\Fewshot.Api.exe"

if (-not $script:SkipService) {
    Write-Step "Registering Windows Service"

    if (-not [System.Diagnostics.EventLog]::SourceExists('Fewshot')) {
        [System.Diagnostics.EventLog]::CreateEventSource('Fewshot', 'Application')
        Write-Ok "Registered 'Fewshot' Event Log source"
    }

    # Service was already stopped earlier; just delete if it exists
    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        sc.exe delete $ServiceName | Out-Null
        for ($i = 0; $i -lt 15; $i++) {
            $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
            if (-not $svc) { break }
            Start-Sleep -Seconds 1
        }
        if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
            Write-Warn "Service still marked for deletion -- a reboot may be needed."
        }
    }

    sc.exe create $ServiceName `
        binPath= "`"$apiExe`"" `
        start= auto `
        DisplayName= "Fewshot AI Middleware" | Out-Null

    sc.exe description $ServiceName "Fewshot local AI middleware -- personalized context for Claude Desktop" | Out-Null
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
    Write-Host "      To start Fewshot manually: $apiExe" -ForegroundColor Gray
}

# ── Wait for API ready ─────────────────────────────────────────────────────────
Write-Step "Waiting for Fewshot API"
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
    Write-Warn "API did not respond at http://127.0.0.1:$ApiPort/health -- check logs after install."
} else {
    Write-Ok "API is responding (status: $($health.status))"
}

# ── Seed default projects ──────────────────────────────────────────────────────
if ($ready) {
    Write-Step "Seeding default projects"

    $defaultProjects = @(
        @{ name='general';   displayName='General';          keywords='general, misc, scratch';            facts=$null },
        @{ name='fewshot';      displayName='Fewshot';             keywords='fewshot, mcp, middleware, ai, ollama'; facts='Local AI middleware project. Stack: .NET 8, SQLite, Ollama.' },
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

# Normalize path to forward slashes to avoid tab/escape bugs in JSON
$mcpExe = ("$InstallDir\mcp\Fewshot.Mcp.exe").Replace('\', '/')
$claudeConfigDir  = "$env:APPDATA\Claude"
$claudeConfigFile = "$claudeConfigDir\claude_desktop_config.json"

New-Item -ItemType Directory -Force -Path $claudeConfigDir | Out-Null

# Back up existing config before any modifications
$backupFile = $null
if (Test-Path $claudeConfigFile) {
    $backupFile = "$claudeConfigFile.bak.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -Path $claudeConfigFile -Destination $backupFile -Force
    Write-Ok "Backed up existing config to $backupFile"
}

try {
    if (Test-Path $claudeConfigFile) {
        # Read with .NET to avoid BOM/encoding issues from Get-Content
        $rawJson = [System.IO.File]::ReadAllText($claudeConfigFile, [System.Text.Encoding]::UTF8).Trim()
        $existing = $rawJson | ConvertFrom-Json

        # StrictMode-safe property probe; PSCustomObject (not hashtable) so ConvertTo-Json serializes it
        if (-not $existing.PSObject.Properties['mcpServers']) {
            $existing | Add-Member -MemberType NoteProperty -Name mcpServers -Value ([pscustomobject]@{})
        }
        $existing.mcpServers | Add-Member -MemberType NoteProperty -Name fewshot -Value ([pscustomobject]@{
            command = $mcpExe
            args    = @()
        }) -Force

        $newJson = $existing | ConvertTo-Json -Depth 10
        # Write WITHOUT BOM -- critical for Claude Desktop compatibility
        [System.IO.File]::WriteAllText($claudeConfigFile, $newJson.Trim(), [System.Text.UTF8Encoding]::new($false))
        Write-Ok "Merged fewshot MCP entry into existing claude_desktop_config.json"
    } else {
        $config = @{
            mcpServers = @{
                fewshot = @{
                    command = $mcpExe
                    args    = @()
                }
            }
        }
        $newJson = $config | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($claudeConfigFile, $newJson.Trim(), [System.Text.UTF8Encoding]::new($false))
        Write-Ok "Created claude_desktop_config.json"
    }
} catch {
    Write-Warn "Failed to update Claude Desktop config: $_"
    if ($backupFile -and (Test-Path $backupFile)) {
        Copy-Item -Path $backupFile -Destination $claudeConfigFile -Force
        Write-Warn "Restored config from backup"
    }
}

# ── Add to PATH ────────────────────────────────────────────────────────────────
Write-Step "Updating PATH"
$currentPath = [System.Environment]::GetEnvironmentVariable('PATH', 'User')
if ($currentPath -notlike "*$InstallDir\api*") {
    [System.Environment]::SetEnvironmentVariable('PATH', "$currentPath;$InstallDir\api", 'User')
    Write-Ok "Added $InstallDir\api to user PATH"
} else {
    Write-Ok "PATH already contains Fewshot api directory"
}

# ── Summary ────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "  ==============================================" -ForegroundColor DarkCyan
Write-Host "   Fewshot installation complete!" -ForegroundColor Green
Write-Host "  ==============================================" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "   API:        http://localhost:$ApiPort" -ForegroundColor White
Write-Host "   Swagger:    http://localhost:$ApiPort/swagger" -ForegroundColor White
Write-Host "   Hangfire:   http://localhost:$ApiPort/hangfire" -ForegroundColor White
if ($installDashboard) {
    Write-Host "   Dashboard:  http://localhost:$ApiPort" -ForegroundColor White
}
Write-Host ""
Write-Host "   Next steps:" -ForegroundColor Yellow
Write-Host "   1. Restart Claude Desktop to activate the MCP tools" -ForegroundColor White
Write-Host "   2. In Claude, start a session with:" -ForegroundColor White
Write-Host "      'Call fewshot_get_context with hint <what you are working on>'" -ForegroundColor Gray
Write-Host ""
if ($script:SkipService) {
    Write-Host "   To run Fewshot as a service, re-run this script as Administrator." -ForegroundColor Yellow
    Write-Host ""
}
Write-Host "   To uninstall: irm https://raw.githubusercontent.com/$RepoOwner/$RepoName/main/uninstall.ps1 | iex" -ForegroundColor Gray
Write-Host ""
