# API changes
cd C:\Users\starkj\source\repos\jstarkwv\APEX\
Stop-Service APEX
dotnet build src\Apex.Api\Apex.Api.csproj -c Release
if ($LASTEXITCODE -ne 0) { Write-Host "API build FAILED - service NOT restarted" -ForegroundColor Red; exit 1 }
Start-Service APEX

# MCP changes
dotnet publish src\Apex.Mcp\Apex.Mcp.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\APEX\mcp"
if ($LASTEXITCODE -ne 0) { Write-Host "MCP publish FAILED" -ForegroundColor Red; exit 1 }
# Then FULLY quit Claude Desktop from the system tray and reopen