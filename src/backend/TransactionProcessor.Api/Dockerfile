# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Build arguments for configuration
ARG BUILD_CONFIGURATION=Release
ARG ASPNETCORE_ENVIRONMENT=Production

WORKDIR /src

# Copy project files FIRST for layer caching optimization
# This layer is cached unless project dependencies change
COPY TransactionProcessor.Domain/TransactionProcessor.Domain.csproj TransactionProcessor.Domain/
COPY TransactionProcessor.Application/TransactionProcessor.Application.csproj TransactionProcessor.Application/
COPY TransactionProcessor.Infrastructure/TransactionProcessor.Infrastructure.csproj TransactionProcessor.Infrastructure/
COPY TransactionProcessor.Api/TransactionProcessor.Api.csproj TransactionProcessor.Api/
COPY TransactionProcessor.Worker/TransactionProcessor.Worker.csproj TransactionProcessor.Worker/

# Restore dependencies (cached layer)
RUN dotnet restore TransactionProcessor.Api/TransactionProcessor.Api.csproj

# Copy source code AFTER restore for better cache hits
COPY . .

# Build and publish
WORKDIR /src/TransactionProcessor.Api
RUN dotnet publish TransactionProcessor.Api.csproj \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:EnvironmentName=${ASPNETCORE_ENVIRONMENT}

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks and remove package cache to keep image slim
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd --create-home --home-dir /app --shell /sbin/nologin appuser

# Copy published output
COPY --from=build /app/publish .

# Adjust permissions and drop privileges
RUN chown -R appuser:appuser /app
USER appuser

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "TransactionProcessor.Api.dll"]
