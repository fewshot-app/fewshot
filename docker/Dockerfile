FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first for layer caching
COPY src/Apex.Core/Apex.Core.csproj Apex.Core/
COPY src/Apex.Infrastructure/Apex.Infrastructure.csproj Apex.Infrastructure/
COPY src/Apex.Api/Apex.Api.csproj Apex.Api/
RUN dotnet restore Apex.Api/Apex.Api.csproj

# Copy everything and build
COPY src/ .
RUN dotnet publish Apex.Api/Apex.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Copy migration scripts for init
COPY src/Apex.Infrastructure/Data/Migrations/ /app/migrations/

EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Apex.Api.dll"]
