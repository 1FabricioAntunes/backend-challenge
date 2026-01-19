# LocalStack Secrets Manager Initialization

This directory contains initialization scripts for setting up AWS Secrets Manager secrets in LocalStack for local development.

## Scripts

### `init-aws.sh`

Main initialization script that:

- Creates S3 buckets (cnab-files)
- Creates SQS queues with DLQ (file-processing-queue, notification-queue)
- Sets up CloudWatch alarms
- Calls `init-secrets.sh` to initialize secrets

### `init-secrets.sh`

Secrets Manager initialization script that creates/updates all application secrets:

#### Database Secrets

- `TransactionProcessor/Database/ConnectionString`
  - PostgreSQL connection string for the transactionprocessor database

#### AWS S3 Secrets

- `TransactionProcessor/AWS/S3` (JSON object with all S3 configuration)
- Individual secrets:
  - `TransactionProcessor/AWS/S3/BucketName` → `cnab-files`
  - `TransactionProcessor/AWS/S3/AccessKeyId` → `test`
  - `TransactionProcessor/AWS/S3/SecretAccessKey` → `test`
  - `TransactionProcessor/AWS/S3/Region` → `us-east-1`

#### AWS SQS Secrets

- `TransactionProcessor/AWS/SQS` (JSON object with all SQS configuration)
- Individual secrets:
  - `TransactionProcessor/AWS/SQS/QueueUrl` → `http://localhost:4566/000000000000/file-processing-queue`
  - `TransactionProcessor/AWS/SQS/DlqUrl` → `http://localhost:4566/000000000000/file-processing-dlq`
  - `TransactionProcessor/AWS/SQS/Region` → `us-east-1`

#### OAuth Secrets (Optional)

- `TransactionProcessor/OAuth` (JSON object with OAuth configuration)
  - ClientId, ClientSecret, Authority, Audience

## Hierarchical Naming Convention

All secrets follow the pattern: `TransactionProcessor/{Component}/{SecretName}`

This provides:

- **Namespace isolation**: All app secrets under TransactionProcessor prefix
- **Component grouping**: Secrets organized by component (Database, AWS/S3, AWS/SQS, OAuth)
- **Clear ownership**: Easy to identify which secrets belong to which service

## Idempotency

The `init-secrets.sh` script is idempotent:

- If a secret doesn't exist, it creates it
- If a secret already exists, it updates it with the current value
- Safe to run multiple times without errors

## Usage

### Automatic Initialization

Scripts run automatically when LocalStack container starts (via Docker Compose).

### Manual Execution

To manually run the initialization:

```bash
# Access the LocalStack container
docker exec -it localstack bash

# Run the secrets initialization
bash /etc/localstack/init/ready.d/init-secrets.sh
```

### Verify Secrets

To list all secrets in LocalStack:

```bash
# Using awslocal CLI
awslocal secretsmanager list-secrets

# Filter TransactionProcessor secrets
awslocal secretsmanager list-secrets \
  --query "SecretList[?starts_with(Name, 'TransactionProcessor/')].Name"
```

### Retrieve a Secret

To retrieve a specific secret value:

```bash
# Get database connection string
awslocal secretsmanager get-secret-value \
  --secret-id "TransactionProcessor/Database/ConnectionString" \
  --query SecretString \
  --output text

# Get S3 secrets (JSON)
awslocal secretsmanager get-secret-value \
  --secret-id "TransactionProcessor/AWS/S3" \
  --query SecretString \
  --output text | jq .
```

## Docker Compose Integration

The scripts are mounted into LocalStack via volume mount in `docker-compose.yml`:

```yaml
localstack:
  volumes:
    - ./src/infra/localstack-init:/etc/localstack/init/ready.d
```

LocalStack automatically executes all scripts in `/etc/localstack/init/ready.d/` when the service becomes ready.

## Production Considerations

⚠️ **Important**: These are development secrets only!

For production:

1. Create secrets manually in AWS Secrets Manager console or via IaC (Terraform, CloudFormation)
2. Use strong, randomly generated values (not "test" or "postgres")
3. Enable secret rotation policies
4. Restrict IAM access with least-privilege policies
5. Enable CloudTrail logging for secret access auditing
6. Use AWS KMS customer-managed keys for encryption

## Troubleshooting

### Secrets not loading

1. Check LocalStack container logs: `docker logs localstack`
2. Verify scripts are mounted: `docker exec localstack ls -la /etc/localstack/init/ready.d/`
3. Manually run init-secrets.sh to see error output

### Connection errors

- Ensure LocalStack is running: `docker ps | grep localstack`
- Verify service is healthy: `docker inspect localstack | grep Health`
- Check LocalStack endpoint: `http://localhost:4566`

### API not loading secrets

1. Check API logs for secret retrieval errors
2. Verify SecretsManagerService configuration in appsettings
3. Confirm ServiceUrl points to LocalStack: `http://localhost:4566`
4. Check correlation ID in logs to trace secret loading

## Files

- `init-aws.sh` - Main AWS resource initialization (S3, SQS, CloudWatch, calls secrets init)
- `init-secrets.sh` - Secrets Manager initialization (creates all app secrets)
- `README.md` - This documentation file
