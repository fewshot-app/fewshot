# API changes
cd C:\Users\starkj\source\repos\jstarkwv\StarkTrace\
Stop-Service StarkTrace
dotnet publish src\StarkTrace.Api\StarkTrace.Api.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\StarkTrace\api"
if ($LASTEXITCODE -ne 0) { Write-Host "API publish FAILED - service NOT restarted" -ForegroundColor Red; exit 1 }
Start-Service StarkTrace

# MCP changes
dotnet publish src\StarkTrace.Mcp\StarkTrace.Mcp.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\StarkTrace\mcp"
if ($LASTEXITCODE -ne 0) { Write-Host "MCP publish FAILED" -ForegroundColor Red; exit 1 }
# Then FULLY quit Claude Desktop from the system tray and reopen