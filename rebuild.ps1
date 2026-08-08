# API changes
cd C:\Users\starkj\source\repos\fewshot-app\fewshot\
Stop-Service Fewshot
dotnet publish src\Fewshot.Api\Fewshot.Api.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\Fewshot\api"
if ($LASTEXITCODE -ne 0) { Write-Host "API publish FAILED - service NOT restarted" -ForegroundColor Red; exit 1 }
Start-Service Fewshot

# MCP changes
dotnet publish src\Fewshot.Mcp\Fewshot.Mcp.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\Fewshot\mcp"
if ($LASTEXITCODE -ne 0) { Write-Host "MCP publish FAILED" -ForegroundColor Red; exit 1 }
# Then FULLY quit Claude Desktop from the system tray and reopen