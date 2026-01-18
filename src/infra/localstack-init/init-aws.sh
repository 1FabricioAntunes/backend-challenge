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

echo "[init] LocalStack AWS resource initialization complete."
