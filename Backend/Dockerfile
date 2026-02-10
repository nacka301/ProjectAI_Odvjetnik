# ========================================
# Multi-stage Dockerfile for .NET Backend
# Target: Render.com (reads PORT env var)
# ========================================

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY Backend.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app

# Minimal system deps (fonts + TLS)
RUN apt-get update && apt-get install -y --no-install-recommends \
    fonts-liberation ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Render provides PORT env var; ASP.NET reads ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Backend.dll"]
