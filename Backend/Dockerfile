# ========================================
# Optimized Multi-stage Dockerfile for .NET Backend
# Target: Render.com (reads PORT env var)
# Alpine images ≈ 3× smaller → deploy 2-3 min instead of 7
# ========================================

# --- Stage 1: Build (Alpine SDK ≈ 500 MB vs 830 MB Debian) ---
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy csproj and restore first (layer caching — only re-runs if deps change)
COPY Backend.csproj .
RUN dotnet restore

# Copy everything else and publish (Release, trimmed output)
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Stage 2: Runtime (Alpine ASP.NET ≈ 100 MB vs 220 MB Debian) ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render provides PORT env var; ASP.NET reads ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

# Required for Alpine (no ICU libraries)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

EXPOSE 8080

ENTRYPOINT ["dotnet", "Backend.dll"]
