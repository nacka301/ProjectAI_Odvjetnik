# ========================================
# Multi-stage Dockerfile for .NET Backend + Playwright
# Target: Render.com (reads PORT env var)
# Debian bookworm — Playwright needs glibc + Chromium deps
# ========================================

# --- Stage 1: Build + install Playwright browsers ---
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

COPY Backend.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Install Playwright Chromium using the bundled Node.js + CLI
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN if [ -d /app/publish/.playwright/package ]; then \
      NODE_BIN=$(find /app/publish/.playwright/node -name node -type f 2>/dev/null | head -1) && \
      if [ -n "$NODE_BIN" ]; then \
        chmod +x "$NODE_BIN" && \
        "$NODE_BIN" /app/publish/.playwright/package/cli.js install --with-deps chromium; \
      fi; \
    fi

# --- Stage 2: Runtime (Debian slim + Chromium dependencies) ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime
WORKDIR /app

# Install Chromium system dependencies needed by Playwright
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 libatk1.0-0 libatk-bridge2.0-0 libcups2 libdrm2 \
    libxkbcommon0 libxcomposite1 libxdamage1 libxfixes3 \
    libxrandr2 libgbm1 libpango-1.0-0 libcairo2 libasound2 \
    libatspi2.0-0 libwayland-client0 \
    fonts-liberation ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY --from=build /ms-playwright /ms-playwright

# Playwright looks here for installed browsers
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

# Render provides PORT env var; ASP.NET reads ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "Backend.dll"]
