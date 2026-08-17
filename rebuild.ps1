# API changes
cd C:\Users\starkj\source\repos\fewshot-app\fewshot\
Stop-Service Fewshot -ErrorAction SilentlyContinue
# Wait for the exe to actually exit (in-flight Hangfire jobs delay shutdown past
# the SCM timeout), then force-kill so publish never hits a locked file.
# NOTE: Hangfire storage is in-memory, so a killed in-flight job is lost —
# re-trigger consolidation (fewshot_end_day) after restart if one was running.
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Process Fewshot.Api -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
Get-Process Fewshot.Api -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish src\Fewshot.Api\Fewshot.Api.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\Fewshot\api"
if ($LASTEXITCODE -ne 0) { Write-Host "API publish FAILED - service NOT restarted" -ForegroundColor Red; exit 1 }
Start-Service Fewshot

# MCP changes
dotnet publish src\Fewshot.Mcp\Fewshot.Mcp.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\Fewshot\mcp"
if ($LASTEXITCODE -ne 0) { Write-Host "MCP publish FAILED" -ForegroundColor Red; exit 1 }
# Then FULLY quit Claude Desktop from the system tray and reopen