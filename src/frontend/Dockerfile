# syntax=docker/dockerfile:1

# Build stage
FROM node:22-alpine AS build
WORKDIR /app

# Build arguments
ARG NODE_ENV=production
ARG VITE_API_URL=http://localhost:5000

WORKDIR /app

# Set environment variables
ENV NODE_ENV=${NODE_ENV}
ENV VITE_API_URL=${VITE_API_URL}

# Copy package files FIRST for layer caching optimization
# This layer is cached unless dependencies change
COPY package*.json ./

# Install dependencies (cached layer)
# Conditionally install devDependencies based on NODE_ENV
# Note: devDependencies (typescript, vite) are required for the build step
# If NODE_ENV != production (e.g., development), include dev deps; otherwise skip them
RUN if [ "$NODE_ENV" != "production" ]; then \
        npm ci --ignore-scripts; \
    else \
        npm ci --only=production --ignore-scripts; \
    fi

# Copy source code AFTER dependency installation
COPY . .

# Build application
RUN npm run build

# Runtime stage
FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html

RUN apk add --no-cache wget \
    && rm -f /etc/nginx/conf.d/default.conf

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist ./

# Prepare non-root execution
# Create directories and set permissions for nginx to run as non-root user
RUN addgroup -S web && adduser -S -G web web \
    && mkdir -p /var/cache/nginx /var/run /var/log/nginx /run /tmp \
    && chown -R web:web /usr/share/nginx/html /var/cache/nginx /var/run /var/log/nginx /run /tmp \
    && touch /var/run/nginx.pid \
    && chown web:web /var/run/nginx.pid

USER web
EXPOSE 3000

HEALTHCHECK --interval=30s --timeout=5s --retries=3 --start-period=15s CMD wget --quiet --tries=1 --spider http://localhost:3000/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
