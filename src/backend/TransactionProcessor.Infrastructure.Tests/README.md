# Infrastructure Integration Tests

Integration tests for TransactionProcessor infrastructure layer using Testcontainers, PostgreSQL, and LocalStack (S3/SQS).

## Project Structure

```text
TransactionProcessor.Infrastructure.Tests/
├── Fixtures/
│   ├── DatabaseFixture.cs       # PostgreSQL container + migrations
│   └── LocalStackFixture.cs     # AWS services emulation (S3/SQS)
├── Tests/
│   ├── Repository/              # Database repository tests
│   ├── Storage/                 # S3 integration tests
│   ├── Messaging/               # SQS integration tests
│   └── API/                     # HTTP endpoint tests
├── Helpers/
│   └── TestDataBuilder.cs       # Test data factories
├── IntegrationTestBase.cs       # Base class for all integration tests
└── TransactionProcessor.Infrastructure.Tests.csproj
```

## Fixtures

### DatabaseFixture

Manages PostgreSQL container lifecycle and provides database operations.

**Features**:

- Automatically starts/stops PostgreSQL container
- Applies EF Core migrations on initialization
- Provides DbContext for test queries
- Utility methods for data clearing and raw SQL execution

**Usage**:

```csharp
public class MyTest : IntegrationTestBase
{
    [Fact]
    public async Task TestDatabaseOperation()
    {
        // DbContext automatically available from base class
        var store = new Store { /* ... */ };
        DbContext.Stores.Add(store);
        await DbContext.SaveChangesAsync();
    }
}
```

### LocalStackFixture

Manages LocalStack container for AWS services emulation (S3, SQS).

**Features**:

- Automatically starts/stops LocalStack container
- Creates S3 bucket on initialization
- Creates SQS queue on initialization
- Provides S3 and SQS clients for test operations
- Utility methods for clearing resources between tests

**Usage**:

```csharp
public class MyTest : IntegrationTestBase
{
    [Fact]
    public async Task TestS3Upload()
    {
        // S3Client automatically available from base class
        await S3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = S3BucketName,
            Key = "test-key",
            InputStream = new MemoryStream(/* content */)
        });
    }
}
```

## IntegrationTestBase

Base class for all integration tests that require both database and LocalStack.

**Provides**:

- Automatic fixture initialization via `InitializeAsync()`
- Automatic fixture cleanup via `DisposeAsync()`
- Convenience properties for `DbContext`, `S3Client`, `SqsClient`
- Helper method `ClearAllTestDataAsync()` for test isolation

**Important**: The base class implements `IAsyncLifetime`, which means fixtures are automatically set up and torn down by xUnit for each test.

## Test Categories

### Repository Tests (Tests/Repository/)

Tests for data persistence layer using real PostgreSQL.

**Examples**:

- `StoreRepositoryTests.cs` - Store CRUD operations
- `TransactionRepositoryTests.cs` - Transaction queries and filtering
- `FileRepositoryTests.cs` - File status management

**Best Practices**:

- Test against real database, not mocks
- Use realistic test data
- Verify data isolation between tests (call `ClearAllTestDataAsync()`)
- Test query performance with reasonable data volumes

### Storage Tests (Tests/Storage/)

Tests for S3 integration using LocalStack.

**Examples**:

- `S3StorageTests.cs` - Upload, download, delete operations
- File size handling
- Concurrent uploads
- Error scenarios

**Best Practices**:

- Use unique keys per test for isolation
- Test various file sizes and types
- Verify error handling (missing buckets, missing keys)
- Clean S3 between tests using `LocalStackFixture.ClearS3BucketAsync()`

### Messaging Tests (Tests/Messaging/)

Tests for SQS integration using LocalStack.

**Examples**:

- `SQSMessagingTests.cs` - Send/receive/delete messages
- Message visibility timeout
- Batch operations
- Dead letter queue (DLQ) behavior

**Best Practices**:

- Verify message content preservation
- Test queue depth and message counts
- Clean queue between tests using `LocalStackFixture.ClearSQSQueueAsync()`
- Test error cases (invalid messages, timeouts)

### API Tests (Tests/API/)

Tests for HTTP endpoints against full stack infrastructure.

**Examples**:

- `FileUploadEndpointTests.cs` - Upload and file processing
- `TransactionQueryEndpointTests.cs` - Query with filters and pagination
- `StoreBalanceEndpointTests.cs` - Store aggregation queries

**Best Practices**:

- Prefer real HTTP calls over mocks
- Test complete workflows (upload → process → query)
- Verify response schemas and status codes
- Test error handling and validation

## Running Tests

### All Tests

```bash
dotnet test src/backend/TransactionProcessor.Infrastructure.Tests.csproj
```

### Specific Category

```bash
# Repository tests
dotnet test --filter "FullyQualifiedName~Repository"

# Storage tests
dotnet test --filter "FullyQualifiedName~Storage"

# Messaging tests
dotnet test --filter "FullyQualifiedName~Messaging"

# API tests
dotnet test --filter "FullyQualifiedName~API"
```

### With Coverage

```bash
dotnet test /p:CollectCoverage=true
```

### Verbose Output

```bash
dotnet test -v normal
```

## Test Data

### Using TestDataBuilder

The `TestDataBuilder` class provides factory methods for creating test data:

```csharp
// Create test store
var store = TestDataBuilder.CreateTestStore("Mercado XYZ", "João Silva");

// Create test file
var file = TestDataBuilder.CreateTestFile("cnab-202501.txt", "Uploaded");

// Create test transaction
var transaction = TestDataBuilder.CreateTestTransaction(
    fileId: file.Id,
    storeId: store.Id,
    transactionTypeCode: "1",
    amount: 150.50m
);

// Generate valid CNAB content
var cnabContent = TestDataBuilder.GenerateValidCnabContent(
    storeName: "Test Store",
    transactionCount: 5
);
```

## Test Isolation

### Database Isolation

Each test fixture gets:

- Fresh PostgreSQL database
- Clean schema (migrations applied)
- No leftover data from previous tests

To clear data within a test:

```csharp
await ClearAllTestDataAsync();
```

### LocalStack Isolation

Each test fixture gets:

- Fresh LocalStack container
- New S3 bucket
- New SQS queue

Use helper methods to clear resources:

```csharp
await LocalStackFixture.ClearS3BucketAsync();
await LocalStackFixture.ClearSQSQueueAsync();
```

## Debugging Tests

### View Container Logs

Testcontainers provides container logs for debugging:

```csharp
// In fixture, after starting container
var logs = await _container.GetLogsAsync();
Console.WriteLine(logs);
```

### Connect to PostgreSQL During Test

Get connection string and use external tools:

```csharp
// Available in test via ConnectionString property
var connString = ConnectionString;  // Use in pgAdmin, DBeaver, etc.
```

### Inspect LocalStack State

LocalStack provides a web UI:

```text
http://localhost:4566  (after container starts)
```

## Performance Considerations

### Container Startup Time

First test run: ~15-30 seconds (container startup)
Subsequent runs: ~3-5 seconds (container reuse if parallel execution)

### Database Migrations

Migrations run once per test fixture (during `InitializeAsync()`)

- Typically 1-2 seconds
- Parallelizable: Each test gets isolated database

### Test Timeouts

Default xUnit timeout: None (recommended for integration tests)
For long-running tests, set explicit timeout:

```csharp
[Fact(Timeout = 60000)]  // 60 seconds
public async Task LongRunningTest() { /* ... */ }
```

## Common Issues

### "Port Already in Use"

Testcontainers may fail if ports are occupied.
Solution: Let Testcontainers use random ports (default behavior)

### Connection Refused

PostgreSQL container not fully started.
Solution: Testcontainers waits for readiness; if issue persists, increase timeout or check logs.

### Tests Hang

Infinite loops or missing async/await.
Solution: Check test code for proper async patterns; use test timeout.

### "Cannot Connect to LocalStack"

LocalStack endpoint not available.
Solution: Verify container started; check S3/SQS configuration in fixture.

## References

- [Testcontainers.NET Documentation](https://testcontainers.com/docs/languages/dotnet/)
- [Entity Framework Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
