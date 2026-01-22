# Troubleshooting Guide

This document provides solutions to common issues encountered during development and deployment.

## Table of Contents

- [Database Issues](#database-issues)
- [LocalStack Issues](#localstack-issues)
- [API Issues](#api-issues)
- [Frontend Issues](#frontend-issues)
- [Docker Issues](#docker-issues)
- [Colima Issues](#colima-issues-docker-alternative)
- [Authentication Issues](#authentication-issues)
- [Performance Issues](#performance-issues)
- [Deployment Issues](#deployment-issues)

## Database Issues

### Cannot Connect to Database

**Symptoms:**
- API fails to start
- Error: "Connection refused" or "Could not connect to server"

**Solutions:**

1. **Verify PostgreSQL is running**:
   ```bash
   docker-compose ps db
   # or
   sudo systemctl status postgresql
   ```

2. **Check connection string in `appsettings.json`**:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres"
     }
   }
   ```

3. **Test database connectivity**:
   ```bash
   psql -h localhost -U postgres -d transactionprocessor
   ```

4. **Check Docker network**:
   ```bash
   docker network ls
   docker network inspect <network-name>
   ```

5. **Verify database logs**:
   ```bash
   docker-compose logs db
   ```

### Migration Errors

**Symptoms:**
- Error: "A migration has already been applied"
- Error: "Could not find migration"

**Solutions:**

1. **Check migration history**:
   ```bash
   dotnet ef migrations list --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
   ```

2. **Rollback to previous migration**:
   ```bash
   dotnet ef database update <PreviousMigrationName> --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
   ```

3. **Remove problematic migration**:
   ```bash
   dotnet ef migrations remove --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
   ```

4. **Regenerate database**:
   ```bash
   docker-compose down -v
   docker-compose up -d db
   dotnet ef database update --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
   ```

### Database Performance Issues

**Symptoms:**
- Slow query responses
- High CPU usage on database container

**Solutions:**

1. **Check for missing indexes**:
   ```sql
   SELECT * FROM pg_stat_user_tables WHERE idx_scan = 0 AND seq_scan > 0;
   ```

2. **Analyze query performance**:
   ```sql
   EXPLAIN ANALYZE SELECT * FROM transactions WHERE store_id = 'xxx';
   ```

3. **Increase connection pool size** in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres;Maximum Pool Size=50"
     }
   }
   ```

## LocalStack Issues

### AWS Services Not Available

**Symptoms:**
- Error: "Could not connect to LocalStack"
- S3 or SQS operations fail

**Solutions:**

1. **Verify LocalStack is running**:
   ```bash
   docker-compose ps localstack
   curl http://localhost:4566/_localstack/health
   ```

2. **Check initialization script**:
   ```bash
   docker-compose logs localstack
   ```

3. **Manually initialize resources**:
   ```bash
   cd src/infra/localstack-init
   chmod +x init-aws.sh
   ./init-aws.sh
   ```

4. **Verify resources were created**:
   ```bash
   aws --endpoint-url=http://localhost:4566 s3 ls
   aws --endpoint-url=http://localhost:4566 sqs list-queues
   ```

5. **Check environment variables**:
   ```bash
   echo $AWS_ACCESS_KEY_ID
   echo $AWS_SECRET_ACCESS_KEY
   echo $AWS_DEFAULT_REGION
   ```

### S3 Bucket Not Found

**Symptoms:**
- Error: "The specified bucket does not exist"

**Solutions:**

1. **Create bucket manually**:
   ```bash
   aws --endpoint-url=http://localhost:4566 s3 mb s3://cnab-files
   ```

2. **Verify bucket exists**:
   ```bash
   aws --endpoint-url=http://localhost:4566 s3 ls
   ```

3. **Check bucket name in configuration**:
   ```json
   {
     "AWS": {
       "S3": {
         "BucketName": "cnab-files",
         "ServiceURL": "http://localhost:4566"
       }
     }
   }
   ```

### SQS Queue Not Found

**Symptoms:**
- Error: "Queue does not exist"

**Solutions:**

1. **Create queue manually**:
   ```bash
   aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name cnab-processing-queue
   ```

2. **List queues**:
   ```bash
   aws --endpoint-url=http://localhost:4566 sqs list-queues
   ```

3. **Update queue URL in configuration**:
   ```json
   {
     "AWS": {
       "SQS": {
         "QueueUrl": "http://localhost:4566/000000000000/cnab-processing-queue",
         "ServiceURL": "http://localhost:4566"
       }
     }
   }
   ```

## API Issues

### API Not Starting

**Symptoms:**
- Port already in use
- Startup errors in logs

**Solutions:**

1. **Check if port 5000 is in use**:
   ```bash
   # Windows
   netstat -ano | findstr :5000
   
   # Linux/Mac
   lsof -i :5000
   ```

2. **Kill process using the port**:
   ```bash
   # Windows
   taskkill /PID <pid> /F
   
   # Linux/Mac
   kill -9 <pid>
   ```

3. **Change the port** in `launchSettings.json`:
   ```json
   {
     "profiles": {
       "TransactionProcessor.Api": {
         "applicationUrl": "http://localhost:5001"
       }
     }
   }
   ```

### CORS Errors

**Symptoms:**
- Frontend cannot call API
- Error: "Access-Control-Allow-Origin"

**Solutions:**

1. **Verify CORS configuration in `Program.cs`**:
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddDefaultPolicy(policy =>
       {
           policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                 .AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials();
       });
   });
   ```

2. **Ensure CORS middleware is before routing**:
   ```csharp
   app.UseCors();
   app.UseRouting();
   ```

3. **Check frontend API base URL**:
   ```typescript
   const API_BASE_URL = 'http://localhost:5000';
   ```

### Authentication Errors

**Symptoms:**
- 401 Unauthorized
- Invalid token errors

**Solutions:**

See [Authentication Issues](#authentication-issues) section below.

## Frontend Issues

### Frontend Not Loading

**Symptoms:**
- Blank page
- Console errors

**Solutions:**

1. **Check dev server is running**:
   ```bash
   cd src/frontend
   pnpm dev
   ```

2. **Clear node_modules and reinstall**:
   ```bash
   rm -rf node_modules
   rm pnpm-lock.yaml
   pnpm install
   ```

3. **Check for TypeScript errors**:
   ```bash
   pnpm tsc --noEmit
   ```

4. **Clear browser cache** or open in incognito mode

### API Calls Failing from Frontend

**Symptoms:**
- Network errors
- CORS errors

**Solutions:**

1. **Verify API URL**:
   ```typescript
   console.log('API Base URL:', import.meta.env.VITE_API_URL);
   ```

2. **Check network tab** in browser DevTools

3. **Test API directly**:
   ```bash
   curl http://localhost:5000/api/stores/v1
   ```

4. **Verify CORS configuration** (see [CORS Errors](#cors-errors))

### Build Errors

**Symptoms:**
- `pnpm build` fails
- TypeScript compilation errors

**Solutions:**

1. **Fix TypeScript errors**:
   ```bash
   pnpm tsc --noEmit
   ```

2. **Update dependencies**:
   ```bash
   pnpm update
   ```

3. **Clear Vite cache**:
   ```bash
   rm -rf node_modules/.vite
   pnpm build
   ```

## Prometheus Issues

### Prometheus Container Unhealthy

**Symptoms:**
- Error: "Container is unhealthy" when starting Prometheus
- Grafana fails to start because it depends on Prometheus
- Prometheus health check fails repeatedly

**Solutions:**

1. **Check Prometheus logs**:
   ```bash
   docker logs prometheus
   docker-compose logs prometheus
   ```

2. **Verify Prometheus configuration file**:
   ```bash
   # Check if config file exists and is valid
   docker exec prometheus promtool check config /etc/prometheus/prometheus.yml
   ```

3. **Test Prometheus health endpoint manually**:
   ```bash
   # From inside the container
   docker exec prometheus wget --quiet --tries=1 --spider http://localhost:9090/-/healthy
   
   # From host
   curl http://localhost:9090/-/healthy
   ```

4. **Check if Prometheus is actually running**:
   ```bash
   docker ps | grep prometheus
   docker exec prometheus ps aux | grep prometheus
   ```

5. **Verify API metrics endpoint is accessible**:
   ```bash
   # Prometheus needs to scrape the API, ensure it's reachable
   docker exec prometheus wget --quiet --tries=1 --spider http://api:5000/api/metrics
   ```

6. **Restart Prometheus with clean state**:
   ```bash
   docker-compose stop prometheus
   docker volume rm transactionprocessor_prometheus-data  # Remove old data
   docker-compose up -d prometheus
   ```

7. **Increase startup time** (if Prometheus needs more time):
   - The health check has a `start_period: 40s` to allow Prometheus to fully start
   - If this is insufficient, you can temporarily increase it in `docker-compose.yml`

8. **Check resource limits**:
   ```bash
   # Prometheus might be OOM killed
   docker stats prometheus
   # If memory usage is high, increase memory limit in docker-compose.yml
   ```

**Common Causes:**
- Invalid Prometheus configuration file
- API metrics endpoint not accessible from Prometheus container
- Insufficient startup time (health check runs too early)
- Resource constraints (memory/CPU limits too low)
- Corrupted Prometheus data directory

**Prevention:**
- The health check uses `wget --spider` which is more reliable than downloading content
- Grafana uses `service_started` instead of `service_healthy` to avoid blocking
- Prometheus has a 40-second startup grace period before health checks begin
- Configuration validation happens automatically on startup

## Docker Issues

### Docker Compose Fails to Start

**Symptoms:**
- Services exit immediately
- Port binding errors

**Solutions:**

1. **Check Docker daemon is running**:
   ```bash
   docker info
   ```

2. **Check for port conflicts**:
   ```bash
   docker-compose ps
   netstat -ano | findstr :5000
   ```

3. **Rebuild images**:
   ```bash
   docker-compose build --no-cache
   docker-compose up
   ```

4. **Check logs for errors**:
   ```bash
   docker-compose logs
   docker-compose logs <service-name>
   ```

### Containers Keep Restarting

**Symptoms:**
- Container exits and restarts repeatedly

**Solutions:**

1. **Check container logs**:
   ```bash
   docker-compose logs --tail=100 <service-name>
   ```

2. **Inspect container**:
   ```bash
   docker inspect <container-id>
   ```

3. **Check health status**:
   ```bash
   docker-compose ps
   ```

4. **Increase memory allocation** in Docker settings

### Volume Mounting Issues

**Symptoms:**
- Code changes not reflected
- Permission errors

**Solutions:**

1. **Verify volume mounts in `docker-compose.yml`**:
   ```yaml
   volumes:
     - ./src:/app/src
   ```

2. **Fix permissions** (Linux/Mac):
   ```bash
   sudo chown -R $USER:$USER ./src
   ```

3. **Restart Docker daemon**

## Colima Issues (Docker Alternative)

If using **Colima** instead of Docker Desktop, see these solutions:

### Colima Not Starting

**Symptoms:**
- Error: "colima: command not found"
- Colima fails to start

**Solutions:**

1. **Install Colima**:
   ```bash
   brew install colima
   ```

2. **Verify installation**:
   ```bash
   colima --version
   ```

3. **Start Colima**:
   ```bash
   colima start
   ```

4. **Check Colima status**:
   ```bash
   colima status
   ```

5. **View Colima logs**:
   ```bash
   colima logs
   ```

### Docker Commands Not Working with Colima

**Symptoms:**
- Error: "Cannot connect to Docker daemon"
- `docker-compose` commands fail

**Solutions:**

1. **Ensure Colima is running**:
   ```bash
   colima start
   ```

2. **Check Docker socket connection**:
   ```bash
   docker info
   ```

3. **Restart Colima**:
   ```bash
   colima stop
   colima start
   ```

4. **Verify Docker is configured for Colima**:
   ```bash
   docker context ls
   docker context use colima
   ```

### High Memory/CPU Usage with Colima

**Symptoms:**
- System slowdown
- Colima using excessive resources

**Solutions:**

1. **Check Colima resource allocation**:
   ```bash
   colima status
   ```

2. **Stop Colima and adjust resources** (in `~/.colima/default/colima.yaml`):
   ```yaml
   cpu: 4
   memory: 8
   disk: 60
   ```

3. **Restart Colima**:
   ```bash
   colima stop
   colima start
   ```

4. **Monitor resource usage**:
   ```bash
   colima stats
   ```

### Colima Volume Mounting Issues

**Symptoms:**
- Shared volumes not working
- Permission errors with mounted directories

**Solutions:**

1. **Verify mount paths**:
   ```bash
   docker run --rm -v /path/to/project:/mnt alpine ls /mnt
   ```

2. **Check Colima mount configuration** in `~/.colima/default/colima.yaml`:
   ```yaml
   mounts:
     - location: ~/projects
       writable: true
   ```

3. **Restart Colima** after config changes:
   ```bash
   colima restart
   ```

4. **Use absolute paths** in docker-compose.yml:
   ```yaml
   volumes:
     - /path/to/local:/container/path
   ```

### Switching Between Docker Desktop and Colima

**Solutions:**

1. **List available contexts**:
   ```bash
   docker context ls
   ```

2. **Switch to Colima**:
   ```bash
   docker context use colima
   ```

3. **Switch back to Docker Desktop**:
   ```bash
   docker context use desktop-linux
   # or
   docker context use default
   ```

4. **All docker-compose commands work the same** with either context

## Authentication Issues

### JWT Token Invalid

**Symptoms:**
- 401 Unauthorized
- Token validation errors

**Solutions:**

1. **Verify token expiration**:
   - Decode JWT at https://jwt.io
   - Check `exp` claim

2. **Check Cognito configuration**:
   ```json
   {
     "Cognito": {
       "UserPoolId": "local_test",
       "ClientId": "test-client",
       "Authority": "http://localhost:4566"
     }
   }
   ```

3. **Refresh token**:
   - Log out and log in again
   - Check token refresh logic

### Cognito User Pool Not Found

**Symptoms:**
- Cannot authenticate users
- Cognito errors in logs

**Solutions:**

1. **Verify user pool exists** (LocalStack):
   ```bash
   aws --endpoint-url=http://localhost:4566 cognito-idp list-user-pools --max-results 10
   ```

2. **Create user pool manually**:
   ```bash
   aws --endpoint-url=http://localhost:4566 cognito-idp create-user-pool --pool-name transaction-processor
   ```

3. **Update configuration** with correct pool ID

## Performance Issues

### Slow API Responses

**Symptoms:**
- High response times
- Timeouts

**Solutions:**

1. **Enable query logging** to identify slow queries

2. **Check database indexes**:
   ```sql
   SELECT * FROM pg_stat_user_indexes;
   ```

3. **Add missing indexes**:
   ```csharp
   modelBuilder.Entity<Transaction>()
       .HasIndex(t => t.StoreId);
   ```

4. **Enable EF Core query splitting**:
   ```csharp
   query.AsSplitQuery()
   ```

5. **Use AsNoTracking for read-only queries**:
   ```csharp
   context.Transactions.AsNoTracking().ToListAsync()
   ```

### High Memory Usage

**Symptoms:**
- Out of memory errors
- Container killed by Docker

**Solutions:**

1. **Increase Docker memory limit**

2. **Optimize queries** to return less data

3. **Use pagination**:
   ```csharp
   query.Skip(skip).Take(pageSize)
   ```

4. **Dispose of DbContext properly**:
   ```csharp
   using (var context = new ApplicationDbContext())
   {
       // ...
   }
   ```

## Deployment Issues

### Lambda Deployment Fails

**Symptoms:**
- SAM deploy errors
- Lambda function not created

**Solutions:**

1. **Check IAM permissions**:
   ```bash
   aws iam get-user
   aws iam list-attached-user-policies --user-name <username>
   ```

2. **Validate SAM template**:
   ```bash
   sam validate
   ```

3. **Check CloudFormation stack events**:
   ```bash
   aws cloudformation describe-stack-events --stack-name <stack-name>
   ```

4. **Review Lambda logs**:
   ```bash
   aws logs tail /aws/lambda/<function-name> --follow
   ```

### RDS Connection Issues (Production)

**Symptoms:**
- Lambda cannot connect to RDS
- Timeout errors

**Solutions:**

1. **Verify VPC configuration**:
   - Lambda and RDS in same VPC
   - Security groups allow traffic

2. **Check security group rules**:
   ```bash
   aws ec2 describe-security-groups --group-ids <sg-id>
   ```

3. **Test connection from Lambda**:
   ```bash
   aws lambda invoke --function-name test-db-connection response.json
   ```

4. **Verify Secrets Manager**:
   ```bash
   aws secretsmanager get-secret-value --secret-id transaction-processor/db
   ```

## Getting Help

If you continue to experience issues:

1. Check [GitHub Issues](https://github.com/your-repo/issues)
2. Review [Documentation](README.md)
3. Contact the development team
4. Review AWS CloudWatch logs for production issues

## Additional Resources

- [Development Guide](development-guide.md)
- [Deployment Guide](deployment.md)
- [Architecture Documentation](architecture.md)
- [Security Documentation](security.md)

---

**Last Updated**: January 14, 2026
