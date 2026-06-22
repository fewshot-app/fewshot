# API changes
cd C:\Users\starkj\source\repos\jstarkwv\APEX\
Stop-Service APEX
dotnet build src\Apex.Api\Apex.Api.csproj -c Release
Start-Service APEX

# MCP changes
dotnet publish src\Apex.Mcp\Apex.Mcp.csproj -c Release -r win-x64 --self-contained -o "C:\Users\starkj\AppData\Local\APEX\mcp"
# Then restart Claude Desktop