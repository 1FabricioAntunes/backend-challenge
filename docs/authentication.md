# Authentication Guide

This document describes how to authenticate with the TransactionProcessor application using LocalStack Cognito.

## Test Credentials

For local development, use the following test credentials:

**Email:** `test@transactionprocessor.local`  
**Password:** `TestPassword123!`

**Note**: The system will attempt to use LocalStack Cognito first. If Cognito is unavailable (LocalStack free tier limitation), it automatically falls back to a mock Cognito service that implements Cognito-compatible authentication and returns valid JWT tokens.

## Authentication Flow

1. **User submits login form** with email and password
2. **Frontend calls** `POST /api/auth/v1/login` with credentials
3. **Backend authenticates** with LocalStack Cognito using `InitiateAuth` API
4. **Cognito returns** JWT access token
5. **Backend returns** token and user information to frontend
6. **Frontend stores** token in localStorage (see security notes)
7. **All subsequent API calls** include token in `Authorization: Bearer <token>` header

## LocalStack Cognito Setup

The Cognito user pool and test user are automatically created when LocalStack starts via Docker Compose.

### Manual Setup (if needed)

If you need to manually set up Cognito:

```bash
# Access LocalStack container
docker exec -it localstack bash

# Run Cognito initialization
bash /etc/localstack/init/ready.d/init-cognito.sh
```

### Verify Cognito Setup

```bash
# List user pools
aws --endpoint-url=http://localhost:4566 cognito-idp list-user-pools --max-results 10

# List users in pool (replace USER_POOL_ID)
aws --endpoint-url=http://localhost:4566 cognito-idp list-users --user-pool-id <USER_POOL_ID>
```

## Creating Additional Test Users

To create additional test users in LocalStack Cognito:

```bash
# Get user pool ID
USER_POOL_ID=$(aws --endpoint-url=http://localhost:4566 cognito-idp list-user-pools \
  --query "UserPools[?Name=='transaction-processor-pool'].Id" \
  --output text)

# Create user
aws --endpoint-url=http://localhost:4566 cognito-idp admin-create-user \
  --user-pool-id "$USER_POOL_ID" \
  --username "newuser@example.com" \
  --user-attributes "Name=email,Value=newuser@example.com" "Name=name,Value=New User" "Name=email_verified,Value=true" \
  --message-action SUPPRESS \
  --temporary-password "TempPassword123!"

# Set permanent password
aws --endpoint-url=http://localhost:4566 cognito-idp admin-set-user-password \
  --user-pool-id "$USER_POOL_ID" \
  --username "newuser@example.com" \
  --password "NewPassword123!" \
  --permanent
```

## Security Notes

⚠️ **Development Only**: These credentials are for local development only.

- Tokens are stored in localStorage (vulnerable to XSS)
- Test credentials are hardcoded in initialization scripts
- LocalStack Cognito is not production-ready

For production:
- Use AWS Cognito (not LocalStack)
- Implement HttpOnly cookies for token storage
- Use strong, randomly generated passwords
- Enable MFA (Multi-Factor Authentication)
- Implement proper password policies

See [docs/security.md](security.md) for production security considerations.

## Troubleshooting

### Login Fails with 401

1. **Verify Cognito is running:**
   ```bash
   docker ps | grep localstack
   ```

2. **Check user exists:**
   ```bash
   aws --endpoint-url=http://localhost:4566 cognito-idp list-users \
     --user-pool-id <USER_POOL_ID>
   ```

3. **Verify password is set:**
   ```bash
   aws --endpoint-url=http://localhost:4566 cognito-idp admin-get-user \
     --user-pool-id <USER_POOL_ID> \
     --username test@transactionprocessor.local
   ```

4. **Check backend logs** for authentication errors

### User Pool Not Found

If the user pool doesn't exist, run the initialization script:

```bash
docker exec -it localstack bash /etc/localstack/init/ready.d/init-cognito.sh
```

### Token Validation Fails

1. **Check Cognito configuration** in `appsettings.Development.json`:
   ```json
   {
     "AWS": {
       "Cognito": {
         "Authority": "http://localhost:4566",
         "ClientId": "test-client",
         "Audience": "transactionprocessor-local"
       }
     }
   }
   ```

2. **Verify JWT middleware** is configured in `Program.cs`

3. **Check token format** - should be a valid JWT with `iss`, `aud`, `exp` claims

## API Endpoints

### POST /api/auth/v1/login

Authenticate user and receive JWT token.

**Request:**
```json
{
  "email": "test@transactionprocessor.local",
  "password": "TestPassword123!"
}
```

**Response (200 OK):**
```json
{
  "user": {
    "name": "Test User",
    "email": "test@transactionprocessor.local"
  },
  "token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Error Responses:**
- `400 Bad Request`: Invalid request (missing/invalid email or password)
- `401 Unauthorized`: Invalid credentials
- `500 Internal Server Error`: Server error

## References

- [Security Documentation](security.md)
- [Backend Documentation](backend.md)
- [Technical Decisions](../technical-decisions.md)
