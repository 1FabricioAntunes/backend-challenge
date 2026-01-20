# Docker Build Guide

This document describes how to build and optimize Docker images for the TransactionProcessor API.

## Prerequisites

- Docker 20.10+ with BuildKit support
- Docker Buildx (for multi-platform builds)

## BuildKit Usage

### Enable BuildKit

**Option 1: Environment Variable (Recommended)**

```bash
export DOCKER_BUILDKIT=1
docker build -t transactionprocessor-api .
```

**Option 2: Docker Daemon Configuration**

Add to `/etc/docker/daemon.json` (Linux) or Docker Desktop settings:

```json
{
  "features": {
    "buildkit": true
  }
}
```

**Option 3: Use buildx (modern Docker)**

```bash
docker buildx build -t transactionprocessor-api .
```

### BuildKit Benefits

- **Parallel builds**: Multiple stages build concurrently
- **Layer caching**: Efficient cache with build secrets support
- **Smaller images**: Automatic cache mount and optimization
- **Build secrets**: Inject secrets without leaving traces

## Build Arguments

Customize the build with `--build-arg`:

```bash
docker build \
  --build-arg BUILD_CONFIGURATION=Debug \
  --build-arg ASPNETCORE_ENVIRONMENT=Development \
  -t transactionprocessor-api:dev .
```

### Available Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `BUILD_CONFIGURATION` | `Release` | .NET build configuration (Release/Debug) |
| `ASPNETCORE_ENVIRONMENT` | `Production` | ASP.NET Core environment |

## Multi-Platform Builds

### Setup Buildx Builder

```bash
# Create a new builder instance
docker buildx create --name multiplatform --use

# Inspect builder
docker buildx inspect --bootstrap
```

### Build for Multiple Platforms

**Linux AMD64 + ARM64:**

```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t myregistry/transactionprocessor-api:latest \
  --push \
  .
```

**Common Platforms:**

- `linux/amd64` - Intel/AMD x86_64 (most cloud instances)
- `linux/arm64` - ARM64 (AWS Graviton, Apple Silicon)
- `linux/arm/v7` - ARM 32-bit (Raspberry Pi)

### Local Multi-Platform Test

Build for ARM64 on AMD64 (or vice versa):

```bash
# Build for ARM64 and load into local Docker
docker buildx build \
  --platform linux/arm64 \
  --load \
  -t transactionprocessor-api:arm64 \
  .

# Note: --load only supports single platform
# For multiple platforms, use --push to registry
```

## Layer Caching Optimization

### How Caching Works

1. **Copy .csproj files** → Restore dependencies (cached)
2. **Copy source code** → Build application (invalidates on code changes)
3. **Publish** → Create runtime artifacts

This ensures NuGet packages are only restored when dependencies change.

### Cache Mounts (BuildKit)

For even faster builds with BuildKit:

```dockerfile
# Example: Use cache mount for NuGet packages
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore TransactionProcessor.Api/TransactionProcessor.Api.csproj
```

## Build Examples

### Development Build

```bash
export DOCKER_BUILDKIT=1
docker build \
  --build-arg BUILD_CONFIGURATION=Debug \
  --build-arg ASPNETCORE_ENVIRONMENT=Development \
  -t transactionprocessor-api:dev \
  .
```

### Production Build

```bash
export DOCKER_BUILDKIT=1
docker build \
  --build-arg BUILD_CONFIGURATION=Release \
  --build-arg ASPNETCORE_ENVIRONMENT=Production \
  -t transactionprocessor-api:latest \
  .
```

### CI/CD Build with Multi-Platform

```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg BUILD_CONFIGURATION=Release \
  --build-arg ASPNETCORE_ENVIRONMENT=Production \
  -t myregistry/transactionprocessor-api:$VERSION \
  -t myregistry/transactionprocessor-api:latest \
  --push \
  .
```

## Image Size Optimization

### Current Strategy

- **Build stage**: `mcr.microsoft.com/dotnet/sdk:8.0` (~800MB)
- **Runtime stage**: `mcr.microsoft.com/dotnet/aspnet:8.0` (~220MB)
- **Final image**: ~230MB (with curl for health checks)

### Further Optimizations

**Use Alpine variant** (smaller base image):

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
```

**Remove unnecessary tools** (if health checks not needed):

```dockerfile
# Skip curl installation if using TCP health checks
HEALTHCHECK CMD wget --spider http://localhost:5000/health || exit 1
```

## Troubleshooting

### BuildKit Not Enabled

**Error**: `WARN[0000] No output specified for docker-container driver. Build result will only remain in the build cache.`

**Solution**: Export environment variable:

```bash
export DOCKER_BUILDKIT=1
```

### Multi-Platform Build Fails

**Error**: `multiple platforms feature is currently not supported for docker driver`

**Solution**: Use buildx with a container builder:

```bash
docker buildx create --use
```

### Cache Not Working

**Issue**: Dependencies re-download on every build

**Check**:

1. Ensure `.csproj` files are copied before source code
2. Verify `.dockerignore` excludes `bin/`, `obj/`
3. Check file timestamps (Git preserves them)

## References

- [Docker BuildKit Documentation](https://docs.docker.com/build/buildkit/)
- [Docker Buildx Multi-Platform](https://docs.docker.com/build/building/multi-platform/)
- [.NET Docker Best Practices](https://docs.microsoft.com/en-us/dotnet/core/docker/build-container)

---

**Last Updated**: January 20, 2026
