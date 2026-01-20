# Production Deployment Guide

## Overview

This guide covers deploying the TransactionProcessor application to a production environment using Docker Compose with production overrides.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Environment Configuration](#environment-configuration)
3. [Deployment Steps](#deployment-steps)
4. [Production Considerations](#production-considerations)
5. [Security Hardening](#security-hardening)
6. [Monitoring & Observability](#monitoring--observability)
7. [Backup & Recovery](#backup--recovery)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### System Requirements

- **OS**: Linux (Ubuntu 20.04+ recommended), Windows Server 2019+, or macOS
- **CPU**: 4+ cores (8+ recommended for production)
- **RAM**: 8GB minimum (16GB+ recommended)
- **Storage**: 50GB+ SSD with room for logs and database growth
- **Docker**: 20.10+
- **Docker Compose**: 2.0+

### External Services Required

1. **Database**: AWS RDS PostgreSQL 16+ (or equivalent managed service)
2. **Object Storage**: AWS S3 bucket for CNAB file storage
3. **Message Queue**: AWS SQS queue for async processing
4. **Secrets Manager**: AWS Secrets Manager or HashiCorp Vault
5. **DNS**: Domain name with SSL/TLS certificate
6. **Monitoring**: CloudWatch, Datadog, or equivalent

---

## Environment Configuration

### 1. Create `.env.prod` File

Create a `.env.prod` file in the project root with production configuration:

```bash
# API Version
API_VERSION=1.0.0
FRONTEND_VERSION=1.0.0

# Database Configuration (use managed RDS in production)
DB_CONNECTION_STRING=Host=prod-db.us-east-1.rds.amazonaws.com;Port=5432;Database=transactionprocessor;Username=app_user;Password=<secure-password>

# AWS Configuration
AWS_REGION=us-east-1
S3_BUCKET_NAME=transactionprocessor-files-prod
SQS_QUEUE_URL=https://sqs.us-east-1.amazonaws.com/123456789012/cnab-processing-prod

# API URL (with HTTPS)
API_URL=https://api.transactionprocessor.com

# Data Path
DATA_PATH=/var/lib/transactionprocessor/postgres
```

### 2. Configure Secrets

#### Database Password

```bash
# Create secrets directory
mkdir -p secrets
chmod 700 secrets

# Store database password
echo "your_secure_production_password" > secrets/db_password.txt
chmod 600 secrets/db_password.txt
```

#### AWS Credentials

For production, use IAM roles instead of static credentials:

1. Create an IAM role with policies for S3 and SQS access
2. Attach the role to your EC2 instance or ECS task
3. No need for `AWS_ACCESS_KEY_ID` or `AWS_SECRET_ACCESS_KEY`

**Required IAM Policies**:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject",
        "s3:ListBucket"
      ],
      "Resource": [
        "arn:aws:s3:::transactionprocessor-files-prod",
        "arn:aws:s3:::transactionprocessor-files-prod/*"
      ]
    },
    {
      "Effect": "Allow",
      "Action": [
        "sqs:SendMessage",
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueAttributes"
      ],
      "Resource": "arn:aws:sqs:us-east-1:123456789012:cnab-processing-prod"
    },
    {
      "Effect": "Allow",
      "Action": [
        "secretsmanager:GetSecretValue"
      ],
      "Resource": "arn:aws:secretsmanager:us-east-1:123456789012:secret:prod/transactionprocessor/*"
    }
  ]
}
```

---

## Deployment Steps

### Quick Deploy (Automated)

```bash
# Make deploy script executable
chmod +x deploy.sh

# Deploy to production
./deploy.sh prod
```

### Manual Deploy

```bash
# Load environment variables
export $(cat .env.prod | grep -v '^#' | xargs)

# Pull latest images (if using registry)
docker-compose -f docker-compose.yml -f docker-compose.prod.yml pull

# Build images
docker-compose -f docker-compose.yml -f docker-compose.prod.yml build --no-cache

# Stop existing containers
docker-compose -f docker-compose.yml -f docker-compose.prod.yml down

# Start services
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Check status
docker-compose -f docker-compose.yml -f docker-compose.prod.yml ps

# View logs
docker-compose -f docker-compose.yml -f docker-compose.prod.yml logs -f api
```

### Verify Deployment

```bash
# Check health endpoints
curl https://api.transactionprocessor.com/health
curl https://api.transactionprocessor.com/health/details

# Check frontend
curl https://transactionprocessor.com

# Check database connectivity
docker-compose -f docker-compose.yml -f docker-compose.prod.yml exec api \
  dotnet ef database update --connection "$DB_CONNECTION_STRING"
```

---

## Production Considerations

### 1. TLS/HTTPS Configuration

**Option A: Nginx Reverse Proxy**

Create `nginx/nginx.conf`:

```nginx
upstream backend {
    server api:5000;
}

server {
    listen 80;
    server_name api.transactionprocessor.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name api.transactionprocessor.com;

    ssl_certificate /etc/nginx/ssl/fullchain.pem;
    ssl_certificate_key /etc/nginx/ssl/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;

    location / {
        proxy_pass http://backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Option B: AWS Application Load Balancer**

1. Create ALB with HTTPS listener
2. Configure ACM certificate
3. Target group pointing to EC2 instance(s) on port 5000
4. Enable health checks: `/health`

### 2. Database Migration

**Switch to Managed Database (AWS RDS)**:

```bash
# 1. Create RDS PostgreSQL instance
aws rds create-db-instance \
    --db-instance-identifier transactionprocessor-prod \
    --db-instance-class db.t3.medium \
    --engine postgres \
    --engine-version 16.1 \
    --master-username admin \
    --master-user-password <secure-password> \
    --allocated-storage 100 \
    --storage-type gp3 \
    --backup-retention-period 7 \
    --multi-az \
    --publicly-accessible false

# 2. Update .env.prod with RDS endpoint
DB_CONNECTION_STRING=Host=transactionprocessor-prod.c9akciq32.us-east-1.rds.amazonaws.com;...

# 3. Run migrations
docker-compose -f docker-compose.yml -f docker-compose.prod.yml run --rm api \
    dotnet ef database update
```

### 3. AWS Services Setup

#### S3 Bucket

```bash
# Create bucket
aws s3 mb s3://transactionprocessor-files-prod --region us-east-1

# Enable versioning
aws s3api put-bucket-versioning \
    --bucket transactionprocessor-files-prod \
    --versioning-configuration Status=Enabled

# Enable encryption
aws s3api put-bucket-encryption \
    --bucket transactionprocessor-files-prod \
    --server-side-encryption-configuration '{
        "Rules": [{
            "ApplyServerSideEncryptionByDefault": {
                "SSEAlgorithm": "AES256"
            }
        }]
    }'

# Configure lifecycle policy (optional)
aws s3api put-bucket-lifecycle-configuration \
    --bucket transactionprocessor-files-prod \
    --lifecycle-configuration file://s3-lifecycle.json
```

#### SQS Queue

```bash
# Create standard queue
aws sqs create-queue \
    --queue-name cnab-processing-prod \
    --attributes '{
        "VisibilityTimeout": "300",
        "MessageRetentionPeriod": "1209600",
        "ReceiveMessageWaitTimeSeconds": "10"
    }'

# Create dead-letter queue
aws sqs create-queue \
    --queue-name cnab-processing-prod-dlq \
    --attributes '{
        "MessageRetentionPeriod": "1209600"
    }'

# Configure DLQ redrive policy
aws sqs set-queue-attributes \
    --queue-url https://sqs.us-east-1.amazonaws.com/123456789012/cnab-processing-prod \
    --attributes '{
        "RedrivePolicy": "{\"deadLetterTargetArn\":\"arn:aws:sqs:us-east-1:123456789012:cnab-processing-prod-dlq\",\"maxReceiveCount\":\"3\"}"
    }'
```

### 4. Remove LocalStack Dependency

In production, `docker-compose.prod.yml` excludes LocalStack using profiles. The API automatically uses real AWS endpoints when `AWS__S3__ServiceURL` and `AWS__SQS__ServiceURL` are empty or not set.

### 5. Scaling Considerations

**Horizontal Scaling (Multiple Replicas)**:

```yaml
# In docker-compose.prod.yml
api:
  deploy:
    replicas: 3  # Run 3 API instances
```

**Vertical Scaling (Resource Limits)**:

```yaml
api:
  deploy:
    resources:
      limits:
        cpus: '8.0'
        memory: 4G
      reservations:
        cpus: '4.0'
        memory: 2G
```

**Migrate to Kubernetes** (recommended for large-scale):

```bash
# Convert to Kubernetes manifests
kompose convert -f docker-compose.yml -f docker-compose.prod.yml

# Or use Helm chart (to be created)
helm install transactionprocessor ./helm/transactionprocessor
```

---

## Security Hardening

### 1. Network Security

- Use VPC with private subnets for database and application
- Only expose ALB/reverse proxy to internet
- Configure security groups to restrict traffic
- Enable VPC Flow Logs

### 2. Secrets Management

**Migrate to AWS Secrets Manager**:

```bash
# Store database password
aws secretsmanager create-secret \
    --name prod/transactionprocessor/db/password \
    --secret-string "your_secure_password"

# Update application to read from Secrets Manager
# Add to appsettings.Production.json:
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Username=...;Password={{resolve:secretsmanager:prod/transactionprocessor/db/password}}"
  }
}
```

### 3. Container Security

- Scan images for vulnerabilities: `docker scan transactionprocessor-api:latest`
- Use minimal base images (already implemented with Alpine)
- Run as non-root user (already implemented)
- Drop unnecessary capabilities (already configured)

### 4. Application Security

- Enable CORS with specific origins
- Configure rate limiting
- Enable request validation
- Use parameterized queries (already done with EF Core)
- Implement audit logging for sensitive operations

---

## Monitoring & Observability

### 1. Application Logs

**CloudWatch Logs (AWS)**:

```bash
# Install CloudWatch agent
sudo wget https://s3.amazonaws.com/amazoncloudwatch-agent/linux/amd64/latest/amazon-cloudwatch-agent.deb
sudo dpkg -i amazon-cloudwatch-agent.deb

# Configure log groups
aws logs create-log-group --log-group-name /transactionprocessor/api
aws logs create-log-group --log-group-name /transactionprocessor/frontend
```

Configure Docker logging driver:

```yaml
api:
  logging:
    driver: awslogs
    options:
      awslogs-region: us-east-1
      awslogs-group: /transactionprocessor/api
      awslogs-stream: api-container
```

### 2. Metrics

**CloudWatch Metrics**:

- Custom metrics from Prometheus endpoint
- Container metrics (CPU, memory, disk, network)
- Application metrics (request rate, duration, errors)

### 3. Alerting

Configure CloudWatch Alarms:

```bash
# High CPU alert
aws cloudwatch put-metric-alarm \
    --alarm-name transactionprocessor-high-cpu \
    --alarm-description "Alert when CPU exceeds 80%" \
    --metric-name CPUUtilization \
    --namespace AWS/ECS \
    --statistic Average \
    --period 300 \
    --threshold 80 \
    --comparison-operator GreaterThanThreshold \
    --evaluation-periods 2

# High error rate
aws cloudwatch put-metric-alarm \
    --alarm-name transactionprocessor-high-errors \
    --metric-name 5XXError \
    --namespace AWS/ApplicationELB \
    --statistic Sum \
    --period 60 \
    --threshold 10 \
    --comparison-operator GreaterThanThreshold \
    --evaluation-periods 1
```

---

## Backup & Recovery

### 1. Database Backups

**Automated RDS Snapshots**:

```bash
# Enable automated backups (retention: 30 days)
aws rds modify-db-instance \
    --db-instance-identifier transactionprocessor-prod \
    --backup-retention-period 30 \
    --preferred-backup-window "03:00-04:00"

# Manual snapshot
aws rds create-db-snapshot \
    --db-instance-identifier transactionprocessor-prod \
    --db-snapshot-identifier transactionprocessor-manual-$(date +%Y%m%d)
```

### 2. File Storage Backup

S3 versioning is already enabled. Configure cross-region replication for disaster recovery:

```bash
aws s3api put-bucket-replication \
    --bucket transactionprocessor-files-prod \
    --replication-configuration file://replication-config.json
```

### 3. Disaster Recovery Plan

1. **RTO (Recovery Time Objective)**: < 1 hour
2. **RPO (Recovery Point Objective)**: < 15 minutes

**Recovery Steps**:

```bash
# 1. Restore database from snapshot
aws rds restore-db-instance-from-db-snapshot \
    --db-instance-identifier transactionprocessor-prod-restored \
    --db-snapshot-identifier <snapshot-id>

# 2. Update connection string
# 3. Redeploy application
./deploy.sh prod

# 4. Verify functionality
curl https://api.transactionprocessor.com/health
```

---

## Troubleshooting

### Common Issues

#### 1. Containers Not Starting

```bash
# Check logs
docker-compose -f docker-compose.yml -f docker-compose.prod.yml logs api

# Check resource usage
docker stats

# Restart specific service
docker-compose -f docker-compose.yml -f docker-compose.prod.yml restart api
```

#### 2. Database Connection Errors

```bash
# Test connectivity
docker-compose exec api bash
apt-get update && apt-get install -y postgresql-client
psql "$DB_CONNECTION_STRING"

# Check security group rules
aws ec2 describe-security-groups --group-ids sg-xxxxx
```

#### 3. AWS Service Errors

```bash
# Check IAM role permissions
aws iam get-role-policy --role-name transactionprocessor-role --policy-name transactionprocessor-policy

# Test S3 access
aws s3 ls s3://transactionprocessor-files-prod

# Test SQS access
aws sqs send-message --queue-url $SQS_QUEUE_URL --message-body "test"
```

#### 4. Performance Issues

```bash
# Check container resource usage
docker stats

# Scale up API instances
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d --scale api=5

# Check database connections
SELECT count(*) FROM pg_stat_activity WHERE state = 'active';
```

---

## Production Deployment Checklist

- [ ] Create and configure `.env.prod` file
- [ ] Set up AWS RDS database
- [ ] Create and configure S3 bucket
- [ ] Create and configure SQS queue
- [ ] Configure IAM roles and policies
- [ ] Store secrets in AWS Secrets Manager
- [ ] Configure TLS/HTTPS with ALB or reverse proxy
- [ ] Set up DNS records
- [ ] Configure firewall and security groups
- [ ] Enable automated database backups
- [ ] Configure log aggregation (CloudWatch Logs)
- [ ] Set up monitoring and alerting
- [ ] Configure WAF and DDoS protection
- [ ] Run database migrations
- [ ] Deploy application using `./deploy.sh prod`
- [ ] Verify health endpoints
- [ ] Test API functionality
- [ ] Test frontend access
- [ ] Document custom configurations
- [ ] Set up on-call rotation and runbooks

---

## Next Steps

1. **CI/CD Pipeline**: Automate builds and deployments with GitHub Actions or AWS CodePipeline
2. **Blue-Green Deployment**: Implement zero-downtime deployments
3. **Auto-scaling**: Configure based on CPU/memory/request metrics
4. **CDN**: Use CloudFront for frontend static assets
5. **API Gateway**: Consider AWS API Gateway for advanced features (throttling, caching)
6. **Multi-Region**: Deploy to multiple AWS regions for high availability

---

## Support

For production issues:
- Check [Troubleshooting Guide](troubleshooting.md)
- Review CloudWatch Logs and Metrics
- Contact DevOps team: devops@transactionprocessor.com
- On-call: PagerDuty incident escalation

For security incidents:
- security@transactionprocessor.com
- Follow incident response playbook
