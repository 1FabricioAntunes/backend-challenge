#!/usr/bin/env bash
set -euo pipefail

echo "[init] Starting LocalStack AWS resource initialization..."

# Region/account defaults for LocalStack
AWS_REGION=${AWS_DEFAULT_REGION:-us-east-1}
ACCOUNT_ID="000000000000"

echo "[init] Ensuring S3 bucket: cnab-files"
if ! awslocal s3 ls "s3://cnab-files" >/dev/null 2>&1; then
  awslocal s3 mb s3://cnab-files --region "$AWS_REGION"
  echo "[init] Created bucket: cnab-files"
else
  echo "[init] Bucket already exists: cnab-files"
fi

echo "[init] Ensuring SQS DLQ: file-processing-dlq"
awslocal sqs create-queue \
  --queue-name file-processing-dlq \
  --attributes MessageRetentionPeriod=1209600,VisibilityTimeout=300 \
  >/dev/null 2>&1 || true

DLQ_ARN="arn:aws:sqs:${AWS_REGION}:${ACCOUNT_ID}:file-processing-dlq"

echo "[init] Ensuring SQS queue: file-processing-queue with DLQ redrive policy"
awslocal sqs create-queue \
  --queue-name file-processing-queue \
  --attributes RedrivePolicy='{"deadLetterTargetArn":"'"${DLQ_ARN}"'","maxReceiveCount":"3"}',VisibilityTimeout=300,MessageRetentionPeriod=1209600,ReceiveMessageWaitTimeSeconds=20 \
  >/dev/null 2>&1 || true

echo "[init] Ensuring SQS DLQ: notification-dlq"
NOTIFICATION_DLQ_URL=$(awslocal sqs create-queue \
  --queue-name notification-dlq \
  --attributes MessageRetentionPeriod=1209600,VisibilityTimeout=300 \
  --query 'QueueUrl' \
  --output text 2>/dev/null) || true

if [ -z "$NOTIFICATION_DLQ_URL" ]; then
  NOTIFICATION_DLQ_URL=$(awslocal sqs get-queue-url \
    --queue-name notification-dlq \
    --query 'QueueUrl' \
    --output text 2>/dev/null) || NOTIFICATION_DLQ_URL=""
fi

if [ -n "$NOTIFICATION_DLQ_URL" ]; then
  echo "[init] Created/verified queue: notification-dlq ($NOTIFICATION_DLQ_URL)"
else
  echo "[init] Warning: Could not create or retrieve notification-dlq URL"
fi

echo "[init] Ensuring SQS queue: notification-queue with DLQ redrive policy"
NOTIFICATION_DLQ_ARN="arn:aws:sqs:${AWS_REGION}:${ACCOUNT_ID}:notification-dlq"
awslocal sqs create-queue \
  --queue-name notification-queue \
  --attributes RedrivePolicy='{"deadLetterTargetArn":"'"${NOTIFICATION_DLQ_ARN}"'","maxReceiveCount":"3"}',VisibilityTimeout=300,MessageRetentionPeriod=1209600,ReceiveMessageWaitTimeSeconds=20 \
  >/dev/null 2>&1 || true

echo "[init] Setting up CloudWatch alarms..."

# CloudWatch alarm for file-processing-dlq (alert if any messages)
echo "[init] Creating CloudWatch alarm: file-processing-dlq-depth"
awslocal cloudwatch put-metric-alarm \
  --alarm-name file-processing-dlq-depth \
  --alarm-description "Alert when file-processing-dlq has messages (DLQ depth > 0)" \
  --metric-name ApproximateNumberOfMessagesVisible \
  --namespace AWS/SQS \
  --statistic Average \
  --period 60 \
  --threshold 0 \
  --comparison-operator GreaterThanThreshold \
  --dimensions Name=QueueName,Value=file-processing-dlq \
  >/dev/null 2>&1 || true

# CloudWatch alarm for notification-dlq (alert if any messages)
echo "[init] Creating CloudWatch alarm: notification-dlq-depth"
awslocal cloudwatch put-metric-alarm \
  --alarm-name notification-dlq-depth \
  --alarm-description "Alert when notification-dlq has messages (DLQ depth > 0)" \
  --metric-name ApproximateNumberOfMessagesVisible \
  --namespace AWS/SQS \
  --statistic Average \
  --period 60 \
  --threshold 0 \
  --comparison-operator GreaterThanThreshold \
  --dimensions Name=QueueName,Value=notification-dlq \
  >/dev/null 2>&1 || true

echo "[init] LocalStack AWS resource initialization complete."
# ============================================================================
# COGNITO INITIALIZATION
# ============================================================================
# Call the Cognito initialization script
if [ -f "/etc/localstack/init/ready.d/init-cognito.sh" ]; then
  echo "[init] Executing Cognito initialization script..."
  bash /etc/localstack/init/ready.d/init-cognito.sh
else
  echo "[init] Warning: init-cognito.sh not found, skipping Cognito initialization"
fi

# ============================================================================
# SECRETS MANAGER INITIALIZATION
# ============================================================================
# Call the secrets initialization script
# This script stores all secrets (including database connection string) in Secrets Manager
# SECURITY: No plain text password files are used - all secrets come from Secrets Manager
# See: docs/security.md § Secrets Management
if [ -f "/etc/localstack/init/ready.d/init-secrets.sh" ]; then
  echo "[init] Executing secrets initialization script..."
  bash /etc/localstack/init/ready.d/init-secrets.sh
  echo "[init] All secrets stored in Secrets Manager (no plain text files)"
else
  echo "[init] Warning: init-secrets.sh not found, skipping secrets initialization"
fi

echo "[init] All LocalStack initialization complete."