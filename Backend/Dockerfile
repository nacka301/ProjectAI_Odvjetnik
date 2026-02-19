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
    fonts-liberation ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN adduser --disabled-password --gecos "" --home /app appuser

COPY --from=build /app/publish .

# Switch to non-root user
USER appuser

# Render provides PORT env var at runtime (typically 10000).
# Do NOT set ASPNETCORE_URLS here — Program.cs reads PORT and binds dynamically.
# Setting it here would hardcode 8080 at build time and conflict with Render's runtime PORT.
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD curl -f http://localhost:10000/api/health || exit 1

ENTRYPOINT ["dotnet", "Backend.dll"]
