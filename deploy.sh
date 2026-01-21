#!/bin/bash

# Deploy Script for TransactionProcessor Production Environment
# Usage: ./deploy.sh [environment]
# Environments: dev, prod

set -e  # Exit on error
set -u  # Exit on undefined variable

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT="${1:-prod}"
COMPOSE_FILES="-f docker-compose.yml"
PROFILE_FLAGS=""

# Functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Environment-specific configuration
case "$ENVIRONMENT" in
    dev)
        log_info "Deploying DEVELOPMENT environment..."
        COMPOSE_FILES="-f docker-compose.yml"
        PROFILE_FLAGS="--profile dev-only"
        ;;
    prod)
        log_info "Deploying PRODUCTION environment..."
        COMPOSE_FILES="-f docker-compose.yml -f docker-compose.prod.yml"
        PROFILE_FLAGS=""
        ;;
    *)
        log_error "Unknown environment: $ENVIRONMENT"
        log_error "Usage: ./deploy.sh [dev|prod]"
        exit 1
        ;;
esac

# Pre-deployment checks
log_info "Running pre-deployment checks..."

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    log_error "Docker is not running. Please start Docker and try again."
    exit 1
fi
log_success "Docker is running"

# Check if docker-compose is available
if ! command -v docker-compose &> /dev/null; then
    log_error "docker-compose is not installed. Please install it and try again."
    exit 1
fi
log_success "docker-compose is available"

# Check if secrets directory exists
if [ ! -d "secrets" ]; then
    log_warning "Secrets directory not found. Creating..."
    mkdir -p secrets
    echo "postgres" > secrets/db_password.txt
    chmod 600 secrets/db_password.txt
    log_success "Created secrets directory with default password"
fi

# Check if .env file exists for production
if [ "$ENVIRONMENT" = "prod" ] && [ ! -f ".env.prod" ]; then
    log_warning ".env.prod file not found. Using default values."
    log_warning "Please create .env.prod with production configuration:"
    log_warning "  - DB_CONNECTION_STRING"
    log_warning "  - AWS_REGION"
    log_warning "  - S3_BUCKET_NAME"
    log_warning "  - SQS_QUEUE_URL"
    log_warning "  - API_URL"
fi

# Load environment variables if .env exists
if [ "$ENVIRONMENT" = "prod" ] && [ -f ".env.prod" ]; then
    log_info "Loading production environment variables from .env.prod"
    export $(cat .env.prod | grep -v '^#' | xargs)
fi

# Pull latest images (production only)
if [ "$ENVIRONMENT" = "prod" ]; then
    log_info "Pulling latest Docker images..."
    docker-compose $COMPOSE_FILES pull --quiet || {
        log_warning "Some images could not be pulled (might be local builds)"
    }
fi

# Build images
log_info "Building Docker images..."
docker-compose $COMPOSE_FILES build --no-cache || {
    log_error "Build failed"
    exit 1
}
log_success "Images built successfully"

# Stop existing containers
log_info "Stopping existing containers..."
docker-compose $COMPOSE_FILES $PROFILE_FLAGS down --remove-orphans || {
    log_warning "No existing containers to stop"
}

# Start services
log_info "Starting services..."
docker-compose $COMPOSE_FILES $PROFILE_FLAGS up -d || {
    log_error "Failed to start services"
    exit 1
}

# Wait for services to be healthy
log_info "Waiting for services to be healthy (this may take a minute)..."
TIMEOUT=120
ELAPSED=0
INTERVAL=5

while [ $ELAPSED -lt $TIMEOUT ]; do
    # Check if all required services are healthy
    UNHEALTHY=$(docker-compose $COMPOSE_FILES ps --format json 2>/dev/null | jq -r '.[] | select(.Health != "healthy" and .Health != "") | .Name' || echo "")
    
    if [ -z "$UNHEALTHY" ]; then
        log_success "All services are healthy!"
        break
    fi
    
    echo -n "."
    sleep $INTERVAL
    ELAPSED=$((ELAPSED + INTERVAL))
done

echo ""

if [ $ELAPSED -ge $TIMEOUT ]; then
    log_warning "Timeout waiting for services to be healthy. Checking status..."
fi

# Display service status
log_info "Service Status:"
docker-compose $COMPOSE_FILES ps

# Health check endpoints
log_info "Performing health checks..."

if [ "$ENVIRONMENT" = "dev" ] || command -v curl &> /dev/null; then
    # Check API health
    log_info "Checking API health endpoint..."
    sleep 5  # Give services a moment to settle
    
    if curl -f -s http://localhost:5000/health > /dev/null 2>&1; then
        log_success "API is healthy (http://localhost:5000/health)"
    else
        log_warning "API health check failed"
    fi
    
    # Check Frontend
    log_info "Checking Frontend..."
    if curl -f -s http://localhost:3000 > /dev/null 2>&1; then
        log_success "Frontend is accessible (http://localhost:3000)"
    else
        log_warning "Frontend is not accessible yet"
    fi
fi

# Display access URLs
echo ""
log_success "Deployment complete!"
echo ""
echo "Access URLs:"
echo "  Frontend:    http://localhost:3000"
echo "  Backend API: http://localhost:5000"
echo "  API Docs:    http://localhost:5000/swagger"
echo "  Health:      http://localhost:5000/health"

if [ "$ENVIRONMENT" = "dev" ]; then
    echo ""
    echo "Development Tools:"
    echo "  Prometheus:  http://localhost:9090"
    echo "  Grafana:     http://localhost:3001 (admin/admin)"
    echo "  LocalStack:  http://localhost:4566"
fi

echo ""
log_info "View logs with: docker-compose $COMPOSE_FILES logs -f"
log_info "Stop services with: docker-compose $COMPOSE_FILES down"

# Production reminders
if [ "$ENVIRONMENT" = "prod" ]; then
    echo ""
    log_warning "Production Deployment Reminders:"
    log_warning "  1. Configure TLS/HTTPS with reverse proxy (nginx/traefik)"
    log_warning "  2. Update DNS records to point to your server"
    log_warning "  3. Configure firewall rules and security groups"
    log_warning "  4. Set up database backups"
    log_warning "  5. Configure external monitoring and alerting"
    log_warning "  6. Review and rotate secrets regularly"
    log_warning "  7. Set up log aggregation"
    log_warning "  8. Configure AWS IAM roles and policies"
    log_warning "  9. Enable WAF and DDoS protection"
    log_warning " 10. Review docker-compose.prod.yml production notes"
fi

exit 0
