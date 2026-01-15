# Development Guide

This document provides detailed instructions for setting up and running the project in a local development environment.

## Prerequisites

### Required Software

- **Docker and Docker Compose** (recommended for quickest setup)
  - *Alternative (no license required)*: **Colima** — Free, open-source container runtime for macOS and Linux
    - Install: `brew install colima`
    - Start: `colima start`
    - Docker CLI still works with Colima as the backend
    - Fully compatible with Docker Compose commands
    - Ideal if Docker Desktop licensing is a concern
- **.NET 8 SDK** (for local development without Docker)
- **Node.js 22+** and **pnpm** or **npm** (for frontend development)
- **PostgreSQL 15** (if running without Docker)
- **Git** (for version control)

### Optional Tools

- **Visual Studio 2022** or **VS Code** (with C# extension)
- **Postman** or **Insomnia** (for API testing)
- **pgAdmin** or **DBeaver** (for database management)

## Quick Start with Docker Compose

The fastest way to run the entire stack locally is using Docker Compose:

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd ByCoders
   ```

2. **Navigate to the src directory**:
   ```bash
   cd src
   ```

3. **Build and start all services**:
   ```bash
   docker-compose up --build
   ```

   This command will:
   - Start PostgreSQL database
   - Start LocalStack (AWS emulation)
   - Initialize AWS resources (S3 bucket, SQS queues)
   - Build and start the backend API
   - Build and start the background worker
   - Build and start the frontend

4. **Access the application**:
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:5000
   - Swagger/OpenAPI: http://localhost:5000/swagger
   - LocalStack: http://localhost:4566

5. **Stop all services**:
   ```bash
   docker-compose down
   ```

6. **Clean up volumes** (removes database data):
   ```bash
   docker-compose down -v
   ```

## Local Development without Docker

For a more granular development experience, you can run each component individually.

### Backend API

1. **Navigate to backend directory**:
   ```bash
   cd src/backend
   ```

2. **Restore NuGet packages**:
   ```bash
   dotnet restore
   ```

3. **Set up the database**:
   
   Ensure PostgreSQL is running locally, then update the connection string in `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres"
     }
   }
   ```

4. **Apply database migrations**:
   ```bash
   dotnet ef database update --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
   ```

5. **Start the API**:
   ```bash
   dotnet run --project TransactionProcessor.Api
   ```

   API available at http://localhost:5000

6. **Run with hot reload** (for development):
   ```bash
   dotnet watch --project TransactionProcessor.Api
   ```

### Background Worker

The worker processes SQS messages asynchronously.

1. **In a new terminal, navigate to backend directory**:
   ```bash
   cd src/backend
   ```

2. **Ensure LocalStack is running** (or use real AWS services):
   ```bash
   docker run --rm -p 4566:4566 localstack/localstack
   ```

3. **Start the worker**:
   ```bash
   dotnet run --project TransactionProcessor.Worker
   ```

4. **Run with hot reload**:
   ```bash
   dotnet watch --project TransactionProcessor.Worker
   ```

### Frontend

1. **Navigate to frontend directory**:
   ```bash
   cd src/frontend
   ```

2. **Install dependencies**:
   ```bash
   pnpm install
   ```
   
   Or with npm:
   ```bash
   npm install
   ```

3. **Start dev server**:
   ```bash
   pnpm dev
   ```
   
   Or with npm:
   ```bash
   npm run dev
   ```

   Frontend available at http://localhost:5173

4. **Build for production**:
   ```bash
   pnpm build
   ```

5. **Preview production build**:
   ```bash
   pnpm preview
   ```

## Environment Configuration

### Backend API Configuration

Update `appsettings.Development.json` with your local settings:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres"
  },
  "AWS": {
    "S3": {
      "BucketName": "cnab-files",
      "ServiceURL": "http://localhost:4566"
    },
    "SQS": {
      "QueueUrl": "http://localhost:4566/000000000000/cnab-processing-queue",
      "ServiceURL": "http://localhost:4566"
    }
  },
  "Cognito": {
    "UserPoolId": "local_test",
    "ClientId": "test-client",
    "Region": "us-east-1",
    "Authority": "http://localhost:4566"
  }
}
```

### Worker Configuration

Update `appsettings.Development.json` in the Worker project:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres"
  },
  "AWS": {
    "S3": {
      "BucketName": "cnab-files",
      "ServiceURL": "http://localhost:4566"
    },
    "SQS": {
      "QueueUrl": "http://localhost:4566/000000000000/cnab-processing-queue",
      "DLQUrl": "http://localhost:4566/000000000000/cnab-processing-dlq",
      "ServiceURL": "http://localhost:4566"
    }
  }
}
```

### Frontend Configuration

Update API base URL in `src/services/api.ts` if needed:

```typescript
const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';
```

Or create a `.env` file:

```
VITE_API_URL=http://localhost:5000
```

## Database Management

### Create Database Manually

If not using Docker, create the database:

```sql
CREATE DATABASE transactionprocessor;
```

### Run Migrations

```bash
cd src/backend
dotnet ef database update --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
```

### Add New Migration

```bash
dotnet ef migrations add <MigrationName> --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
```

### Rollback Migration

```bash
dotnet ef database update <PreviousMigrationName> --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
```

### Remove Last Migration

```bash
dotnet ef migrations remove --project TransactionProcessor.Infrastructure --startup-project TransactionProcessor.Api
```

## Testing

### Run All Tests

```bash
cd src/backend
dotnet test
```

### Run Specific Test Project

```bash
dotnet test tests/Unit/Domain.Tests
```

### Run Tests with Coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Run Tests by Category

```bash
# Unit tests only
dotnet test --filter Category=Unit

# Integration tests only
dotnet test --filter Category=Integration

# E2E tests only
dotnet test --filter Category=E2E
```

## Debugging

### Backend API Debugging

**Visual Studio:**
1. Open `TransactionProcessor.sln`
2. Set `TransactionProcessor.Api` as startup project
3. Press F5 to start debugging

**VS Code:**
1. Open the `src/backend` folder
2. Press F5 and select `.NET Core Launch (web)`

### Frontend Debugging

**Chrome DevTools:**
1. Start the dev server: `pnpm dev`
2. Open Chrome and navigate to http://localhost:5173
3. Press F12 to open DevTools

**VS Code:**
1. Install the "Debugger for Chrome" extension
2. Add a launch configuration
3. Press F5 to start debugging

## LocalStack Setup

LocalStack emulates AWS services locally for development.

### Start LocalStack

```bash
docker run --rm -p 4566:4566 -e SERVICES=s3,sqs,cognito localstack/localstack
```

### Initialize Resources

Run the initialization script:

```bash
cd src/infra/localstack-init
chmod +x init-aws.sh
./init-aws.sh
```

This creates:
- S3 bucket: `cnab-files`
- SQS queue: `cnab-processing-queue`
- SQS DLQ: `cnab-processing-dlq`
- Cognito user pool (for authentication testing)

### Verify Resources

```bash
# List S3 buckets
aws --endpoint-url=http://localhost:4566 s3 ls

# List SQS queues
aws --endpoint-url=http://localhost:4566 sqs list-queues
```

## IDE Configuration

### Visual Studio 2022

Recommended extensions:
- **ReSharper** (optional, for enhanced C# support)
- **CodeMaid** (for code cleanup)

### VS Code

Recommended extensions:
- **C# (ms-dotnettools.csharp)**
- **C# Dev Kit (ms-dotnettools.csdevkit)**
- **REST Client (humao.rest-client)**
- **ESLint (dbaeumer.vscode-eslint)**
- **Prettier (esbenp.prettier-vscode)**
- **Thunder Client (rangav.vscode-thunder-client)**

## Performance Profiling

### Backend Performance

Use dotnet-trace for performance profiling:

```bash
dotnet tool install --global dotnet-trace
dotnet trace collect --process-id <pid>
```

### Frontend Performance

Use Chrome DevTools Performance tab:
1. Open DevTools (F12)
2. Go to Performance tab
3. Click Record
4. Interact with the application
5. Stop recording and analyze

## Common Development Tasks

### Update Dependencies

**Backend:**
```bash
cd src/backend
dotnet list package --outdated
dotnet add package <PackageName> --version <Version>
```

**Frontend:**
```bash
cd src/frontend
pnpm update
# or
npm update
```

### Generate API Client

If using OpenAPI/Swagger code generation:

```bash
cd src/frontend
npx openapi-typescript http://localhost:5000/swagger/v1/swagger.json --output src/types/api.ts
```

### Format Code

**Backend:**
```bash
dotnet format
```

**Frontend:**
```bash
pnpm prettier --write .
```

## Next Steps

- See [Testing Strategy](testing-strategy.md) for testing guidelines
- See [Troubleshooting](troubleshooting.md) for common issues
- See [Deployment](deployment.md) for production deployment

---

**Last Updated**: January 14, 2026
