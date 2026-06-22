#Requires -Version 5.1
<#
.SYNOPSIS
    APEX uninstaller
.EXAMPLE
    irm https://raw.githubusercontent.com/jstarkwv/APEX/main/uninstall.ps1 | iex
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ServiceName = 'APEX'
$InstallDir  = "$env:LOCALAPPDATA\APEX"

function Write-Step { param($msg) Write-Host "`n  --> $msg" -ForegroundColor Cyan }
function Write-Ok   { param($msg) Write-Host "      [OK] $msg" -ForegroundColor Green }
function Write-Warn { param($msg) Write-Host "      [WARN] $msg" -ForegroundColor Yellow }

function Test-Admin {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    ([System.Security.Principal.WindowsPrincipal]$id).IsInRole(
        [System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

Write-Host ""
Write-Host "  APEX Uninstaller" -ForegroundColor DarkCyan
Write-Host ""

$confirm = Read-Host "  This will remove the APEX service, files, and MCP config. Continue? [y/N]"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "  Cancelled." -ForegroundColor Gray
    exit 0
}

$keepData = Read-Host "  Keep your data (SQLite database, memories, preferences)? [Y/n]"
$keepData = $keepData -ne 'n' -and $keepData -ne 'N'

# ── Stop and remove service ────────────────────────────────────────────────────
Write-Step "Removing Windows Service"
if (Test-Admin) {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($svc) {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Write-Ok "Service '$ServiceName' removed"
    } else {
        Write-Ok "Service not found (already removed)"
    }
} else {
    Write-Warn "Not running as Administrator — cannot remove Windows Service."
    Write-Warn "Run 'sc delete APEX' as Administrator to remove it manually."
}

# ── Remove from PATH ───────────────────────────────────────────────────────────
Write-Step "Cleaning PATH"
$currentPath = [System.Environment]::GetEnvironmentVariable('PATH', 'User')
$newPath = ($currentPath -split ';' | Where-Object { $_ -notlike "*$InstallDir*" }) -join ';'
[System.Environment]::SetEnvironmentVariable('PATH', $newPath, 'User')
Write-Ok "Removed APEX from user PATH"

# ── Remove MCP entry from Claude Desktop config ────────────────────────────────
Write-Step "Removing MCP config"
$claudeConfigFile = "$env:APPDATA\Claude\claude_desktop_config.json"
if (Test-Path $claudeConfigFile) {
    try {
        $config = Get-Content $claudeConfigFile -Raw | ConvertFrom-Json
        if ($config.mcpServers -and $config.mcpServers.PSObject.Properties['apex']) {
            $config.mcpServers.PSObject.Properties.Remove('apex')
            $config | ConvertTo-Json -Depth 10 | Set-Content $claudeConfigFile -Encoding UTF8
            Write-Ok "Removed apex entry from claude_desktop_config.json"
        } else {
            Write-Ok "No apex MCP entry found — nothing to remove"
        }
    } catch {
        Write-Warn "Could not update claude_desktop_config.json: $_"
    }
} else {
    Write-Ok "Claude Desktop config not found — nothing to remove"
}

# ── Remove files ───────────────────────────────────────────────────────────────
Write-Step "Removing install directory"
if (Test-Path $InstallDir) {
    if ($keepData) {
        Write-Host "      Keeping database at $InstallDir\apex.db" -ForegroundColor Gray
        Remove-Item -Recurse -Force "$InstallDir\api"       -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force "$InstallDir\mcp"       -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force "$InstallDir\dashboard" -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force "$InstallDir\proxy"     -ErrorAction SilentlyContinue
        Remove-Item -Recurse -Force "$InstallDir\presidio"  -ErrorAction SilentlyContinue
        Write-Ok "Binaries removed. Data preserved at $InstallDir"
    } else {
        Remove-Item -Recurse -Force $InstallDir
        Write-Ok "Install directory and data removed"
    }
} else {
    Write-Ok "Install directory not found — nothing to remove"
}

Write-Host ""
Write-Host "  APEX has been uninstalled." -ForegroundColor Green
if ($keepData) {
    Write-Host "  Your data is preserved at $InstallDir\" -ForegroundColor Gray
}
Write-Host "  Restart Claude Desktop to deactivate the MCP tools." -ForegroundColor Yellow
Write-Host ""
