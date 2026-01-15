# Deployment Guide

This document describes deployment strategies for local development and production environments.

## Overview

The TransactionProcessor system is designed to run in multiple environments:
- **Local**: Docker Compose with LocalStack
- **Production**: AWS Lambda, RDS, S3, SQS, Cognito

## Local Deployment

### Docker Compose (Recommended)

The simplest way to run the entire stack locally.

#### Prerequisites

- Docker 20.10+
- Docker Compose 2.0+
- 4GB RAM minimum
- 10GB disk space

#### Deployment Steps

1. **Navigate to the source directory**:
   ```bash
   cd src
   ```

2. **Start all services**:
   ```bash
   docker-compose up --build
   ```

3. **Verify services are running**:
   ```bash
   docker-compose ps
   ```

   You should see:
   - `db` (PostgreSQL)
   - `localstack` (AWS emulation)
   - `api` (Backend API)
   - `worker` (Background worker)
   - `frontend` (React frontend)

4. **Access the application**:
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger
   - LocalStack: http://localhost:4566

#### Environment Variables

Customize via `docker-compose.yml` or `.env` file:

```env
# Database
POSTGRES_DB=transactionprocessor
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres

# LocalStack
LOCALSTACK_SERVICES=s3,sqs,cognito

# API
ASPNETCORE_ENVIRONMENT=Development
AWS_REGION=us-east-1

# Frontend
VITE_API_URL=http://localhost:5000
```

#### Stop Services

```bash
# Stop without removing containers
docker-compose stop

# Stop and remove containers
docker-compose down

# Stop and remove volumes (wipes database)
docker-compose down -v
```

### Colima (Docker Alternative for macOS/Linux)

If Docker Desktop licensing is a concern, **Colima** is a free, open-source alternative that works seamlessly with Docker Compose.

#### Prerequisites

- Colima 0.5.0+
- 4GB RAM minimum
- 10GB disk space
- macOS (Intel or Apple Silicon) or Linux

#### Setup & Deployment Steps

1. **Install Colima** (via Homebrew):
   ```bash
   brew install colima
   ```

2. **Start Colima**:
   ```bash
   colima start
   ```

   Colima takes about 30 seconds to start. You'll see:
   ```
   INFO   Colima is running.
   INFO   To stop Colima, run `colima stop`
   ```

3. **Verify Colima is running**:
   ```bash
   colima status
   docker info
   ```

4. **Run Docker Compose** (same commands as Docker Desktop):
   ```bash
   cd src
   docker-compose up --build
   ```

5. **Access the application** (same as Docker Compose):
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger
   - LocalStack: http://localhost:4566

#### Resource Configuration

Edit `~/.colima/default/colima.yaml` to adjust CPU, memory, and disk:

```yaml
cpu: 4
memory: 8
disk: 60
```

Restart Colima for changes to take effect:
```bash
colima restart
```

#### Switching Between Docker Runtimes

If you have both Docker Desktop and Colima installed, switch between them:

```bash
# List available contexts
docker context ls

# Switch to Colima
docker context use colima

# Switch to Docker Desktop
docker context use desktop-linux
# or
docker context use default
```

#### Stopping Colima

```bash
# Graceful stop
colima stop

# View logs
colima logs

# Force stop
colima stop --force
```

#### Benefits of Colima

- **No licensing costs** — Completely free and open-source
- **Lower resource usage** — More efficient than Docker Desktop
- **Full compatibility** — All Docker and Docker Compose commands work identically
- **Easy switching** — Can alternate between Docker Desktop and Colima
- **Great for Apple Silicon** — Excellent performance on M1/M2 Macs

#### Troubleshooting Colima

For common Colima issues, see [Troubleshooting Guide - Colima Issues](troubleshooting.md#colima-issues-docker-alternative).

## Production Deployment (AWS)

The system is architected for serverless deployment on AWS.

### Architecture Overview

1. **API**: AWS Lambda + API Gateway
2. **Worker**: AWS Lambda triggered by SQS
3. **Database**: AWS RDS PostgreSQL
4. **Storage**: AWS S3
5. **Messaging**: AWS SQS with DLQ
6. **Authentication**: AWS Cognito
7. **Monitoring**: AWS CloudWatch

### Prerequisites

- AWS Account with appropriate permissions
- AWS CLI configured
- AWS SAM CLI installed
- .NET 8 SDK
- Node.js 22+

### Infrastructure as Code (AWS SAM)

The project uses AWS SAM for infrastructure provisioning.

#### SAM Template Structure

```yaml
# template.yaml (example)
AWSTemplateFormatVersion: '2010-09-09'
Transform: AWS::Serverless-2016-10-31

Resources:
  # API Lambda Function
  ApiFunction:
    Type: AWS::Serverless::Function
    Properties:
      Runtime: dotnet8
      Handler: TransactionProcessor.Api::TransactionProcessor.Api.LambdaEntryPoint::FunctionHandlerAsync
      Environment:
        Variables:
          ConnectionStrings__DefaultConnection: !Sub "{{resolve:secretsmanager:${DBSecret}:SecretString:connectionString}}"
      Events:
        ApiGateway:
          Type: Api
          Properties:
            Path: /{proxy+}
            Method: ANY

  # Worker Lambda Function
  WorkerFunction:
    Type: AWS::Serverless::Function
    Properties:
      Runtime: dotnet8
      Handler: TransactionProcessor.Worker::TransactionProcessor.Worker.LambdaEntryPoint::FunctionHandlerAsync
      Events:
        SQSEvent:
          Type: SQS
          Properties:
            Queue: !GetAtt ProcessingQueue.Arn

  # S3 Bucket
  FilesBucket:
    Type: AWS::S3::Bucket
    Properties:
      BucketName: cnab-files-prod

  # SQS Queue
  ProcessingQueue:
    Type: AWS::SQS::Queue
    Properties:
      QueueName: cnab-processing-queue
      RedrivePolicy:
        deadLetterTargetArn: !GetAtt ProcessingDLQ.Arn
        maxReceiveCount: 3

  # DLQ
  ProcessingDLQ:
    Type: AWS::SQS::Queue
    Properties:
      QueueName: cnab-processing-dlq

  # RDS PostgreSQL
  Database:
    Type: AWS::RDS::DBInstance
    Properties:
      DBInstanceClass: db.t3.micro
      Engine: postgres
      EngineVersion: '15'
      MasterUsername: !Sub "{{resolve:secretsmanager:${DBSecret}:SecretString:username}}"
      MasterUserPassword: !Sub "{{resolve:secretsmanager:${DBSecret}:SecretString:password}}"

  # Cognito User Pool
  UserPool:
    Type: AWS::Cognito::UserPool
    Properties:
      UserPoolName: transaction-processor-users
```

### Deployment Steps

#### 1. Build the Application

```bash
# Build backend
cd src/backend
dotnet publish -c Release -o publish

# Build frontend
cd ../frontend
pnpm build
```

#### 2. Deploy Infrastructure

```bash
# Initialize SAM
sam init

# Build SAM application
sam build

# Deploy to AWS
sam deploy --guided
```

Follow the prompts to configure:
- Stack name
- AWS Region
- Environment (production/staging)
- Database credentials (stored in Secrets Manager)

#### 3. Configure Secrets Manager

Store sensitive configuration in AWS Secrets Manager:

```bash
aws secretsmanager create-secret \
  --name transaction-processor/db \
  --secret-string '{"username":"dbuser","password":"strongpassword","host":"rds-endpoint","database":"transactionprocessor"}'

aws secretsmanager create-secret \
  --name transaction-processor/cognito \
  --secret-string '{"userPoolId":"us-east-1_XXXXX","clientId":"XXXXX"}'
```

#### 4. Run Database Migrations

Connect to the RDS instance and run migrations:

```bash
# Update connection string
export ConnectionStrings__DefaultConnection="Host=<rds-endpoint>;Database=transactionprocessor;Username=dbuser;Password=strongpassword"

# Run migrations
dotnet ef database update --project src/backend/TransactionProcessor.Infrastructure --startup-project src/backend/TransactionProcessor.Api
```

#### 5. Deploy Frontend to S3 + CloudFront

```bash
# Upload to S3
aws s3 sync src/frontend/dist s3://frontend-bucket --delete

# Invalidate CloudFront cache
aws cloudfront create-invalidation --distribution-id <dist-id> --paths "/*"
```

### Post-Deployment Verification

#### Health Checks

```bash
# API Health
curl https://api.yourdomain.com/health

# Check Lambda function
aws lambda invoke --function-name transaction-processor-api response.json
cat response.json
```

#### Monitor Logs

```bash
# API logs
aws logs tail /aws/lambda/transaction-processor-api --follow

# Worker logs
aws logs tail /aws/lambda/transaction-processor-worker --follow
```

#### Test the System

```bash
# Upload a test file
curl -X POST https://api.yourdomain.com/api/files/v1 \
  -H "Authorization: Bearer <token>" \
  -F "file=@test.cnab"

# Query transactions
curl https://api.yourdomain.com/api/transactions/v1
```

## Configuration Management

### Environment-Specific Configuration

**Local (appsettings.Development.json)**:

```json
{
  "AWS": {
    "S3": { "ServiceURL": "http://localhost:4566" },
    "SQS": { "ServiceURL": "http://localhost:4566" }
  }
}
```

**Production (appsettings.Production.json)**:

```json
{
  "AWS": {
    "Region": "us-east-1"
  },
  "ConnectionStrings": {
    "DefaultConnection": "{{resolve:secretsmanager:transaction-processor/db}}"
  }
}
```

### Secrets Management

Use AWS Secrets Manager for all sensitive data:

- Database credentials
- Cognito configuration
- API keys
- Third-party service credentials

Never commit secrets to source control.

## Scaling Configuration

### Lambda Configuration

```yaml
# template.yaml
Resources:
  ApiFunction:
    Properties:
      MemorySize: 512
      Timeout: 30
      ReservedConcurrentExecutions: 10

  WorkerFunction:
    Properties:
      MemorySize: 1024
      Timeout: 300
      ReservedConcurrentExecutions: 5
```

### RDS Scaling

For production workloads:
- Use Multi-AZ deployment for high availability
- Enable automated backups
- Configure read replicas for read-heavy workloads

```yaml
Database:
  Type: AWS::RDS::DBInstance
  Properties:
    DBInstanceClass: db.t3.medium
    MultiAZ: true
    BackupRetentionPeriod: 7
    ReadReplicaSourceDBInstanceIdentifier: !Ref PrimaryDatabase
```

### SQS Configuration

```yaml
ProcessingQueue:
  Properties:
    VisibilityTimeout: 300
    MessageRetentionPeriod: 345600  # 4 days
    ReceiveMessageWaitTimeSeconds: 20  # Long polling
```

## Rollback Strategy

### Lambda Rollback

```bash
# List versions
aws lambda list-versions-by-function --function-name transaction-processor-api

# Rollback to previous version
aws lambda update-alias --function-name transaction-processor-api \
  --name live --function-version <previous-version>
```

### Database Rollback

```bash
# Rollback migration
dotnet ef database update <previous-migration-name> \
  --project src/backend/TransactionProcessor.Infrastructure \
  --startup-project src/backend/TransactionProcessor.Api
```

### CloudFormation Rollback

```bash
# Automatic rollback on stack update failure
sam deploy --no-fail-on-empty-changeset --rollback-on-failure
```

## Monitoring and Alerts

### CloudWatch Alarms

Set up alerts for:
- Lambda errors and throttles
- SQS queue depth (DLQ)
- RDS CPU and memory
- API Gateway 5xx errors

```bash
aws cloudwatch put-metric-alarm \
  --alarm-name high-error-rate \
  --metric-name Errors \
  --namespace AWS/Lambda \
  --statistic Sum \
  --period 60 \
  --threshold 10 \
  --comparison-operator GreaterThanThreshold
```

### X-Ray Tracing

Enable AWS X-Ray for distributed tracing:

```yaml
ApiFunction:
  Properties:
    Tracing: Active
```

## Cost Optimization

### Lambda Optimization

- Right-size memory allocation
- Use Lambda Provisioned Concurrency for predictable workloads
- Optimize cold start times

### S3 Optimization

- Use S3 Lifecycle policies to archive old files
- Enable S3 Intelligent-Tiering

### RDS Optimization

- Use Reserved Instances for production
- Right-size instance types
- Enable storage autoscaling

## Disaster Recovery

### Backup Strategy

- **RDS**: Automated daily backups with 7-day retention
- **S3**: Enable versioning and cross-region replication
- **Database Snapshots**: Manual snapshots before major changes

### Recovery Procedures

1. **RDS Point-in-Time Recovery**:
   ```bash
   aws rds restore-db-instance-to-point-in-time \
     --source-db-instance-identifier prod-db \
     --target-db-instance-identifier prod-db-restored \
     --restore-time 2026-01-14T10:00:00Z
   ```

2. **S3 Object Recovery**:
   ```bash
   aws s3api list-object-versions --bucket cnab-files-prod
   aws s3api get-object --bucket cnab-files-prod --key file.cnab --version-id <version-id> restored-file.cnab
   ```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Deploy to AWS

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Build
        run: dotnet build -c Release
      
      - name: Test
        run: dotnet test
      
      - name: Configure AWS Credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: us-east-1
      
      - name: SAM Build
        run: sam build
      
      - name: SAM Deploy
        run: sam deploy --no-confirm-changeset --no-fail-on-empty-changeset
```

## Troubleshooting Deployment

See [Troubleshooting Guide](troubleshooting.md) for common deployment issues.

## Security Considerations

- Use IAM roles with least privilege
- Enable VPC for Lambda functions
- Use Security Groups to restrict RDS access
- Enable encryption at rest for S3 and RDS
- Use AWS WAF for API Gateway protection

See [Security Documentation](security.md) for detailed security configuration.

---

**Last Updated**: January 14, 2026
