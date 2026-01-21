#!/usr/bin/env bash
set -euo pipefail

echo "[cognito] Starting LocalStack Cognito initialization..."

# Region defaults for LocalStack
AWS_REGION=${AWS_DEFAULT_REGION:-us-east-1}
ACCOUNT_ID="000000000000"

# Test user credentials (for development/demo only)
TEST_USER_EMAIL="test@transactionprocessor.local"
TEST_USER_PASSWORD="TestPassword123!"
TEST_USER_NAME="Test User"

# User pool configuration
USER_POOL_NAME="transaction-processor-pool"
CLIENT_NAME="test-client"  # Must match AWS:Cognito:ClientId in appsettings.Development.json

echo "[cognito] Creating Cognito User Pool: ${USER_POOL_NAME}"

# Create user pool (idempotent - will fail if exists, which is fine)
USER_POOL_ID=$(awslocal cognito-idp create-user-pool \
  --pool-name "${USER_POOL_NAME}" \
  --policies "PasswordPolicy={MinimumLength=6,RequireUppercase=true,RequireLowercase=true,RequireNumbers=true,RequireSymbols=false}" \
  --auto-verified-attributes email \
  --query 'UserPool.Id' \
  --output text 2>/dev/null) || {
  # Pool might already exist, try to get it
  echo "[cognito] User pool might already exist, attempting to retrieve..."
  USER_POOL_ID=$(awslocal cognito-idp list-user-pools \
    --max-results 10 \
    --query "UserPools[?Name=='${USER_POOL_NAME}'].Id" \
    --output text 2>/dev/null | head -n1) || USER_POOL_ID=""
}

if [ -z "$USER_POOL_ID" ]; then
  echo "[cognito] Error: Could not create or find user pool"
  exit 1
fi

echo "[cognito] User Pool ID: ${USER_POOL_ID}"

# Create app client (idempotent)
echo "[cognito] Creating Cognito App Client: ${CLIENT_NAME}"
CLIENT_ID=$(awslocal cognito-idp create-user-pool-client \
  --user-pool-id "${USER_POOL_ID}" \
  --client-name "${CLIENT_NAME}" \
  --generate-secret \
  --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH \
  --query 'UserPoolClient.ClientId' \
  --output text 2>/dev/null) || {
  # Client might already exist, try to get it
  echo "[cognito] App client might already exist, attempting to retrieve..."
  CLIENT_ID=$(awslocal cognito-idp list-user-pool-clients \
    --user-pool-id "${USER_POOL_ID}" \
    --query "UserPoolClients[?ClientName=='${CLIENT_NAME}'].ClientId" \
    --output text 2>/dev/null | head -n1) || CLIENT_ID=""
}

if [ -z "$CLIENT_ID" ]; then
  echo "[cognito] Error: Could not create or find app client"
  exit 1
fi

echo "[cognito] App Client ID: ${CLIENT_ID}"

# Check if test user already exists
echo "[cognito] Checking if test user exists: ${TEST_USER_EMAIL}"
USER_EXISTS=$(awslocal cognito-idp admin-get-user \
  --user-pool-id "${USER_POOL_ID}" \
  --username "${TEST_USER_EMAIL}" \
  --query 'Username' \
  --output text 2>/dev/null) || USER_EXISTS=""

if [ -z "$USER_EXISTS" ]; then
  # Create test user
  echo "[cognito] Creating test user: ${TEST_USER_EMAIL}"
  awslocal cognito-idp admin-create-user \
    --user-pool-id "${USER_POOL_ID}" \
    --username "${TEST_USER_EMAIL}" \
    --user-attributes "Name=email,Value=${TEST_USER_EMAIL}" "Name=name,Value=${TEST_USER_NAME}" "Name=email_verified,Value=true" \
    --message-action SUPPRESS \
    --temporary-password "${TEST_USER_PASSWORD}" \
    >/dev/null 2>&1 || {
    echo "[cognito] Warning: Could not create user (might already exist)"
  }

  # Set permanent password (bypass password policy for test user)
  echo "[cognito] Setting permanent password for test user..."
  awslocal cognito-idp admin-set-user-password \
    --user-pool-id "${USER_POOL_ID}" \
    --username "${TEST_USER_EMAIL}" \
    --password "${TEST_USER_PASSWORD}" \
    --permanent \
    >/dev/null 2>&1 || {
    echo "[cognito] Warning: Could not set permanent password"
  }
else
  echo "[cognito] Test user already exists: ${TEST_USER_EMAIL}"
  # Ensure password is set correctly
  awslocal cognito-idp admin-set-user-password \
    --user-pool-id "${USER_POOL_ID}" \
    --username "${TEST_USER_EMAIL}" \
    --password "${TEST_USER_PASSWORD}" \
    --permanent \
    >/dev/null 2>&1 || true
fi

echo "[cognito] Cognito initialization complete."
echo "[cognito] ========================================="
echo "[cognito] Test User Credentials:"
echo "[cognito]   Email: ${TEST_USER_EMAIL}"
echo "[cognito]   Password: ${TEST_USER_PASSWORD}"
echo "[cognito]   User Pool ID: ${USER_POOL_ID}"
echo "[cognito]   Client ID: ${CLIENT_ID}"
echo "[cognito] ========================================="
