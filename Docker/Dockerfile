# ===========================================
# Stage 1: Base Runtime
# ===========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Tools
    wget \
    ca-certificates \
    # Kerberos for PostgreSQL
    krb5-user \
    libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# ===========================================
# Stage 2: Build
# ===========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files for dependency restore (layer caching optimization)
COPY ["src/AzmCrm.API/AzmCrm.API.csproj", "src/AzmCrm.API/"]
COPY ["src/AzmCrm.Application/AzmCrm.Application.csproj", "src/AzmCrm.Application/"]
COPY ["src/AzmCrm.Domain/AzmCrm.Domain.csproj", "src/AzmCrm.Domain/"]
COPY ["src/AzmCrm.Infrastructure/AzmCrm.Infrastructure.csproj", "src/AzmCrm.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/AzmCrm.API/AzmCrm.API.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/src/AzmCrm.API"
RUN dotnet build "AzmCrm.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# ===========================================
# Stage 3: Publish
# ===========================================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "AzmCrm.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ===========================================
# Stage 4: Final Runtime Image
# ===========================================
FROM base AS final

# Metadata
LABEL maintainer="Azm Tech <support@azmtech.com>" \
      version="1.0.0" \
      description="Azm CRM API - .NET 10" \
      org.opencontainers.image.source="https://github.com/Azm-Tech/azm-crm-backend" \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.title="Azm CRM API" \
      org.opencontainers.image.vendor="Azm Tech"

WORKDIR /app

# Copy published application
COPY --from=publish /app/publish .

# Create non-root user and set permissions
RUN groupadd -r appuser --gid=1001 \
    && useradd -r -g appuser --uid=1001 --home=/app --shell=/sbin/nologin appuser \
    && chown -R appuser:appuser /app

# Environment variables (Production defaults - can be overridden at runtime)
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_USE_POLLING_FILE_WATCHER=true


# Expose ports
EXPOSE 8080

# Health check (using curl or wget)
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

# Switch to non-root user
USER appuser

# Set entry point
ENTRYPOINT ["dotnet", "AzmCrm.API.dll"]
