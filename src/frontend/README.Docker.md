# Frontend Docker Build Guide

This document describes how to build and optimize Docker images for the TransactionProcessor frontend.

## Prerequisites

- Docker 20.10+ with BuildKit support
- Docker Buildx (for multi-platform builds)

## BuildKit Usage

### Enable BuildKit

**Option 1: Environment Variable (Recommended)**

```bash
export DOCKER_BUILDKIT=1
docker build -t transactionprocessor-frontend .
```

**Option 2: Use buildx (modern Docker)**

```bash
docker buildx build -t transactionprocessor-frontend .
```

### BuildKit Benefits

- **Parallel builds**: Faster multi-stage builds
- **Layer caching**: npm packages cached efficiently
- **Smaller images**: Optimized final image size
- **Build cache**: Persistent cache across builds

## Build Arguments

Customize the build with `--build-arg`:

```bash
docker build \
  --build-arg NODE_ENV=production \
  --build-arg VITE_API_URL=https://api.example.com \
  -t transactionprocessor-frontend .
```

### Available Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `NODE_ENV` | `production` | Node environment (production/development) |
| `VITE_API_URL` | `http://localhost:5000` | Backend API URL |

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
  -t myregistry/transactionprocessor-frontend:latest \
  --push \
  .
```

**Common Platforms:**

- `linux/amd64` - Intel/AMD x86_64 (most servers)
- `linux/arm64` - ARM64 (AWS Graviton, Apple Silicon)
- `linux/arm/v7` - ARM 32-bit (edge devices)

## Layer Caching Optimization

### How Caching Works

1. **Copy package.json/package-lock.json** → Install dependencies (cached)
2. **Copy source code** → Build Vite app (invalidates on code changes)
3. **Copy to nginx** → Serve static files

This ensures npm packages are only installed when dependencies change.

### Cache Mounts (BuildKit)

For faster builds with BuildKit:

```dockerfile
# Example: Use cache mount for npm cache
RUN --mount=type=cache,target=/root/.npm \
    npm ci --only=production --ignore-scripts
```

## Build Examples

### Development Build

```bash
export DOCKER_BUILDKIT=1
docker build \
  --build-arg NODE_ENV=development \
  --build-arg VITE_API_URL=http://localhost:5000 \
  -t transactionprocessor-frontend:dev \
  .
```

### Production Build

```bash
export DOCKER_BUILDKIT=1
docker build \
  --build-arg NODE_ENV=production \
  --build-arg VITE_API_URL=https://api.production.com \
  -t transactionprocessor-frontend:latest \
  .
```

### Multi-Platform Production Build

```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg NODE_ENV=production \
  --build-arg VITE_API_URL=https://api.production.com \
  -t myregistry/transactionprocessor-frontend:$VERSION \
  -t myregistry/transactionprocessor-frontend:latest \
  --push \
  .
```

## Image Size Optimization

### Current Strategy

- **Build stage**: `node:20-alpine` (~150MB)
- **Runtime stage**: `nginx:alpine` (~40MB)
- **Final image**: ~45MB (with static assets)

### Further Optimizations

**Remove source maps in production:**

```json
// vite.config.ts
export default defineConfig({
  build: {
    sourcemap: false  // Disable for production
  }
})
```

**Compress assets:**

```dockerfile
# Add gzip compression
RUN apk add --no-cache gzip && \
    find /usr/share/nginx/html -type f \( -name '*.js' -o -name '*.css' \) -exec gzip -k {} \;
```

## Nginx Configuration

### SPA Routing

The nginx.conf includes proper SPA fallback:

```nginx
location / {
    try_files $uri $uri/ /index.html;
}
```

### Caching Strategy

```nginx
location /assets/ {
    expires 7d;
    access_log off;
    add_header Cache-Control "public";
}
```

## Troubleshooting

### Build Fails with "Cannot find module"

**Issue**: Dependencies not installed correctly

**Solution**:

1. Check `package.json` and `package-lock.json` are copied
2. Verify `npm ci` runs before `npm run build`
3. Clear Docker build cache: `docker builder prune`

### Health Check Fails

**Error**: Container restarts continuously

**Check**:

1. Nginx is listening on port 3000
2. `/health` endpoint returns 200
3. `wget` is installed in the image

```bash
# Test health check manually
docker run -it transactionprocessor-frontend sh
wget --quiet --tries=1 --spider http://localhost:3000/health
```

### Multi-Platform Build Slow

**Issue**: ARM64 build takes very long on AMD64 host

**Solution**: Use QEMU emulation or build natively:

```bash
# Install QEMU
docker run --privileged --rm tonistiigi/binfmt --install all

# Or build on ARM64 runner in CI/CD
```

## References

- [Docker BuildKit Documentation](https://docs.docker.com/build/buildkit/)
- [Docker Buildx Multi-Platform](https://docs.docker.com/build/building/multi-platform/)
- [Vite Build Optimization](https://vitejs.dev/guide/build.html)
- [Nginx Docker Best Practices](https://docs.nginx.com/nginx/admin-guide/installing-nginx/installing-nginx-docker/)

---

**Last Updated**: January 20, 2026
