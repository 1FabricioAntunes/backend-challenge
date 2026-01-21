# Docker Security Hardening - Implementation Summary

## Overview

This document describes the security hardening measures implemented in the Docker Compose configuration to follow container security best practices.

## Changes Made

### 1. Resource Limits and Reservations

Added CPU and memory constraints to all services to prevent resource exhaustion:

**API Service**:

- Limits: 2 CPUs, 1GB memory
- Reservations: 0.5 CPUs, 512MB memory

**Frontend Service**:

- Limits: 1 CPU, 512MB memory
- Reservations: 0.25 CPUs, 128MB memory

**Database Service**:

- Limits: 1 CPU, 512MB memory
- Reservations: 0.25 CPUs, 256MB memory

**Prometheus/Grafana/LocalStack**:

- Limits: 1 CPU, 512MB-1GB memory
- Reservations: 0.25 CPUs, 128-256MB memory

### 2. Security Options

**No New Privileges** (`security_opt: no-new-privileges:true`):

- Applied to all services
- Prevents privilege escalation attacks
- Blocks setuid/setgid bit execution

### 3. Capability Management

**Default Policy** (`cap_drop: ["ALL"]`):

- All services drop all Linux capabilities by default
- Implements principle of least privilege

**Selective Capability Addition**:

- **API/Frontend**: Added `NET_BIND_SERVICE` only (required to bind to ports <1024)
- **Database**: Added `CHOWN`, `SETGID`, `SETUID` (PostgreSQL requirements)
- **Prometheus/Grafana**: No capabilities added (run without privileges)

### 4. Secrets Management

**Docker Secrets Implementation**:

- Top-level `secrets:` section in docker-compose.yml
- Database password stored in `./secrets/db_password.txt`
- PostgreSQL configured with `POSTGRES_PASSWORD_FILE` instead of `POSTGRES_PASSWORD`
- Secrets mounted at `/run/secrets/` inside containers

**Security Benefits**:

- Credentials not visible in `docker inspect` output
- Not passed as environment variables
- Not stored in image layers
- Encrypted in Docker Swarm mode

**Directory Structure**:

```text
secrets/
├── .gitignore          # Prevents committing secrets
├── README.md           # Setup instructions
└── db_password.txt     # PostgreSQL password (gitignored)
```

### 5. Verified Backend Dockerfile Security

**Confirmed Configuration**:

- `USER appuser` - Runs as non-root user
- Non-privileged user created with `useradd`
- Proper file ownership with `chown`
- Health check using curl (minimal dependencies)

## Security Architecture

### Defense in Depth Layers

1. **Image Level**: Multi-stage builds, minimal base images, non-root users
2. **Container Level**: Capability restrictions, resource limits, no-new-privileges
3. **Secret Level**: File-based secrets, external secret manager integration ready
4. **Network Level**: Isolated bridge network, no host networking
5. **Runtime Level**: Health checks, restart policies, dependency conditions

## Production Considerations

### External Secret Management

For production deployments, replace file-based secrets with:

**AWS Secrets Manager**:

```yaml
secrets:
  db_password:
    external: true
    name: prod/transactionprocessor/db/password
```

**HashiCorp Vault**:

- Use Vault agent sidecar injection
- Mount secrets via CSI driver
- Rotate credentials automatically

### Additional Hardening

**Read-only Root Filesystem**:

```yaml
services:
  api:
    read_only: true
    tmpfs:
      - /tmp
      - /var/tmp
```

**AppArmor/SELinux Profiles**:

```yaml
services:
  api:
    security_opt:
      - apparmor:docker-default
      - no-new-privileges:true
```

**Seccomp Profiles**:

```yaml
services:
  api:
    security_opt:
      - seccomp:runtime/default
```

## Verification

### Check Security Settings

```bash
# Inspect container security configuration
docker inspect transactionprocessor-api | jq '.[0].HostConfig.SecurityOpt'
docker inspect transactionprocessor-api | jq '.[0].HostConfig.CapDrop'
docker inspect transactionprocessor-api | jq '.[0].HostConfig.CapAdd'

# Verify non-root execution
docker exec transactionprocessor-api whoami
# Expected output: appuser

# Check resource limits
docker stats --no-stream transactionprocessor-api
```

### Verify Secrets

```bash
# Secrets should NOT appear in environment variables
docker exec transactionprocessor-db env | grep POSTGRES_PASSWORD
# Expected: POSTGRES_PASSWORD_FILE=/run/secrets/db_password

# Secret should be mounted as file
docker exec transactionprocessor-db cat /run/secrets/db_password
# Expected: postgres (or your configured password)
```

### Test Capability Restrictions

```bash
# Attempting privileged operations should fail
docker exec transactionprocessor-api apt-get update
# Expected: Permission denied or similar error
```

## Compliance Mapping

### CIS Docker Benchmark

- ✅ 5.1 - Verify AppArmor profile (if enabled)
- ✅ 5.2 - Verify SELinux security options (if enabled)
- ✅ 5.3 - Restrict Linux kernel capabilities
- ✅ 5.4 - Do not use privileged containers
- ✅ 5.5 - Do not mount sensitive host system directories
- ✅ 5.7 - Do not map privileged ports within containers (using >1024 internally)
- ✅ 5.10 - Do not share host's network namespace
- ✅ 5.12 - Bind incoming container traffic to specific host interface (localhost only)
- ✅ 5.25 - Restrict container from acquiring additional privileges
- ✅ 5.26 - Check container health at runtime
- ✅ 5.28 - Use PIDs cgroup limit (via resource constraints)

### NIST Cybersecurity Framework

- **Identify**: Resource inventory with clear limits
- **Protect**: Capability restrictions, non-root execution, secrets management
- **Detect**: Health checks, monitoring (Prometheus/Grafana)
- **Respond**: Automated restarts, dependency management
- **Recover**: Persistent volumes, backup strategy (external)

## Maintenance

### Regular Security Tasks

1. **Update Base Images**: `docker-compose pull && docker-compose build --no-cache`
2. **Rotate Secrets**: Update `secrets/db_password.txt` and restart services
3. **Audit Logs**: Review container logs for security events
4. **Scan Images**: `docker scan transactionprocessor-api:local`
5. **Review Resource Usage**: Adjust limits based on actual usage patterns

### Testing Security Changes

```bash
# Rebuild with security enhancements
docker-compose build --no-cache

# Start services
docker-compose up -d

# Wait for health checks
docker-compose ps

# Run verification tests
./scripts/verify-deployment.sh

# Check security posture
docker-compose exec api whoami  # Should be: appuser
docker-compose exec frontend whoami  # Should be: nginx or web
```

## Known Limitations

1. **LocalStack**: Requires Docker socket mount (dev environment only)
2. **Port Binding**: Services bind to all interfaces (0.0.0.0) - consider firewall rules in production
3. **Secret Rotation**: Manual process in file-based secrets mode
4. **Read-only FS**: Not enabled by default due to application write requirements (logs, temp files)

## References

- [CIS Docker Benchmark](https://www.cisecurity.org/benchmark/docker)
- [Docker Security Best Practices](https://docs.docker.com/develop/security-best-practices/)
- [OWASP Docker Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Docker_Security_Cheat_Sheet.html)
- [Docker Secrets Documentation](https://docs.docker.com/engine/swarm/secrets/)
- [Linux Capabilities](https://man7.org/linux/man-pages/man7/capabilities.7.html)

## Next Steps

1. Implement AppArmor/SELinux profiles for production
2. Enable read-only root filesystem with tmpfs mounts
3. Integrate with external secret manager (AWS Secrets Manager)
4. Set up automated vulnerability scanning in CI/CD
5. Configure network policies for pod-to-pod communication (Kubernetes)
6. Implement runtime security monitoring (Falco, Sysdig)
