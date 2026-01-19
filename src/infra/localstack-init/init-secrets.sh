#!/usr/bin/env bash
set -euo pipefail

echo "[secrets] Starting LocalStack Secrets Manager initialization..."

# Region defaults for LocalStack
AWS_REGION=${AWS_DEFAULT_REGION:-us-east-1}
ACCOUNT_ID="000000000000"

# Helper function to create or update a secret
create_or_update_secret() {
  local secret_name="$1"
  local secret_value="$2"
  
  echo "[secrets] Ensuring secret: ${secret_name}"
  
  # Try to create the secret first
  if awslocal secretsmanager create-secret \
    --name "${secret_name}" \
    --secret-string "${secret_value}" \
    --region "$AWS_REGION" \
    >/dev/null 2>&1; then
    echo "[secrets] Created secret: ${secret_name}"
  else
    # Secret already exists, update it
    if awslocal secretsmanager update-secret \
      --secret-id "${secret_name}" \
      --secret-string "${secret_value}" \
      --region "$AWS_REGION" \
      >/dev/null 2>&1; then
      echo "[secrets] Updated secret: ${secret_name}"
    else
      echo "[secrets] Warning: Could not create or update secret: ${secret_name}"
    fi
  fi
}

# ============================================================================
# DATABASE SECRETS
# ============================================================================
echo "[secrets] Configuring database connection secrets..."

DATABASE_CONNECTION_STRING="Host=db;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres;Include Error Detail=true"

create_or_update_secret \
  "TransactionProcessor/Database/ConnectionString" \
  "${DATABASE_CONNECTION_STRING}"

# ============================================================================
# AWS S3 SECRETS
# ============================================================================
echo "[secrets] Configuring AWS S3 secrets..."

# Create S3 secrets as a JSON object for easier retrieval
S3_SECRETS=$(cat <<EOF
{
  "BucketName": "cnab-files",
  "AccessKeyId": "test",
  "SecretAccessKey": "test",
  "Region": "us-east-1"
}
EOF
)

create_or_update_secret \
  "TransactionProcessor/AWS/S3" \
  "${S3_SECRETS}"

# Also create individual secrets for backward compatibility
create_or_update_secret \
  "TransactionProcessor/AWS/S3/BucketName" \
  "cnab-files"

create_or_update_secret \
  "TransactionProcessor/AWS/S3/AccessKeyId" \
  "test"

create_or_update_secret \
  "TransactionProcessor/AWS/S3/SecretAccessKey" \
  "test"

create_or_update_secret \
  "TransactionProcessor/AWS/S3/Region" \
  "us-east-1"

# ============================================================================
# AWS SQS SECRETS
# ============================================================================
echo "[secrets] Configuring AWS SQS secrets..."

# Create SQS secrets as a JSON object
SQS_SECRETS=$(cat <<EOF
{
  "QueueUrl": "http://localhost:4566/000000000000/file-processing-queue",
  "DlqUrl": "http://localhost:4566/000000000000/file-processing-dlq",
  "Region": "us-east-1"
}
EOF
)

create_or_update_secret \
  "TransactionProcessor/AWS/SQS" \
  "${SQS_SECRETS}"

# Also create individual secrets for backward compatibility
create_or_update_secret \
  "TransactionProcessor/AWS/SQS/QueueUrl" \
  "http://localhost:4566/000000000000/file-processing-queue"

create_or_update_secret \
  "TransactionProcessor/AWS/SQS/DlqUrl" \
  "http://localhost:4566/000000000000/file-processing-dlq"

create_or_update_secret \
  "TransactionProcessor/AWS/SQS/Region" \
  "us-east-1"

# ============================================================================
# OAUTH SECRETS (Optional - for development)
# ============================================================================
echo "[secrets] Configuring OAuth secrets (optional)..."

# Create OAuth secrets as a JSON object (for development/testing only)
OAUTH_SECRETS=$(cat <<EOF
{
  "ClientId": "test-client-id",
  "ClientSecret": "test-client-secret",
  "Authority": "http://localhost:4566/cognito",
  "Audience": "transaction-processor-api"
}
EOF
)

create_or_update_secret \
  "TransactionProcessor/OAuth" \
  "${OAUTH_SECRETS}"

# ============================================================================
# VERIFICATION
# ============================================================================
echo "[secrets] Verifying created secrets..."

# List all secrets for TransactionProcessor
echo "[secrets] Available secrets:"
awslocal secretsmanager list-secrets \
  --query "SecretList[?starts_with(Name, 'TransactionProcessor/')].Name" \
  --output table \
  2>/dev/null || echo "[secrets] Warning: Could not list secrets"

echo "[secrets] LocalStack Secrets Manager initialization complete."
echo "[secrets] All secrets are now available for the TransactionProcessor API."
