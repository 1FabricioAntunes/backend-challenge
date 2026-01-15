# Testing Strategy

This document describes the project's testing strategy, tools, scope, and quality gates.

## Overview

The project adopts a multi-layer testing strategy to ensure code quality, reliability, and maintainability. The strategy follows the test pyramid principle, with more unit tests at the base and fewer end-to-end tests at the top.

## Test Types

### Unit Tests

**Scope**: Domain logic and use cases

- **Domain**: Entities, value objects, aggregates, domain services
- **Application**: Use cases, validators, DTOs
- **Infrastructure**: Repository implementations (with mocks)

**Tools**:
- **xUnit**: Testing framework for .NET
- **Moq**: Mocking framework
- **FluentAssertions**: Readable assertions

**Expected Coverage**: > 80% for domain and application layers

### Integration Tests

**Scope**: Interactions with external systems

- **Database**: Persistence operations, transactions, queries
- **Messaging**: SQS message enqueue and consume
- **Storage**: S3 operations (upload, download)
- **API**: HTTP endpoints (with real or in-memory database)

**Tools**:
- **xUnit**: Testing framework
- **Testcontainers**: Docker containers for tests
- **LocalStack**: Emulation of AWS services (SQS, S3)

**Environment**: Isolated database per test (Testcontainers or in-memory)

### End-to-End (E2E) Tests

**Scope**: Happy path only

- **Complete Flow**: Upload → Processing → Query
- **Main Scenarios**:
    - Successful upload of a valid CNAB file
    - Complete file processing
    - Transaction queries with filters
    - Store balance calculation

**Tools**:
- **xUnit**: Testing framework
- **Testcontainers**: Full infrastructure
- **Playwright** (optional): For UI tests

**Limitation**: Happy path only to avoid excessive complexity

## Tools

### Backend (.NET)

- **xUnit**: Unit and integration testing framework
- **Moq**: Mocking framework
- **FluentAssertions**: Readable and expressive assertions
- **Testcontainers**: Docker containers for integration tests
- **LocalStack**: AWS services emulation

### Frontend (React)

- **Jest**: Testing framework
- **React Testing Library**: Component tests
- **MSW (Mock Service Worker)**: API mocking

### E2E

- **xUnit + Testcontainers**: For backend E2E tests
- **Playwright** (optional): For UI E2E tests

## Test Structure

```
tests/
├── Unit/
│   ├── Domain.Tests/          # Domain tests
│   ├── Application.Tests/     # Use case tests
│   └── Infrastructure.Tests/  # Infrastructure tests (with mocks)
├── Integration/
│   ├── Database.Tests/        # Database tests
│   ├── Messaging.Tests/       # SQS tests
│   ├── Storage.Tests/         # S3 tests
│   └── Api.Tests/             # Endpoint tests
└── E2E/
    └── HappyPath.Tests/       # End-to-end tests
```

## Test Scope

### What Is Tested

#### Domain
- ✅ Entity validation
- ✅ Business rules
- ✅ Value objects
- ✅ Aggregates and invariants
- ✅ Domain services

#### Application
- ✅ Use cases
- ✅ Validators
- ✅ DTO mappings
- ✅ Flow orchestration

#### Infrastructure
- ✅ Repositories (with real database)
- ✅ SQS integration
- ✅ S3 integration
- ✅ Configurations

#### API
- ✅ HTTP endpoints
- ✅ Request validation
- ✅ Error handling
- ✅ Authentication/authorization

### What Is NOT Tested (Deliberately)

- ❌ All possible error scenarios (only main ones)
- ❌ Load/performance tests (out of scope)
- ❌ Advanced security tests (only basics)
- ❌ Complex UI tests (only happy path)

## Happy Path in E2E Tests

### Main Scenarios

1. **Successful Upload and Processing**
   ```
   - Upload valid CNAB file
   - Verify "Uploaded" status
   - Wait for processing
   - Verify "Processed" status
   - Verify persisted transactions
   ```

2. **Transaction Query**
   ```
   - Query without filters
   - Query with store filter
   - Query with date filter
   - Verify results
   ```

3. **Balance Calculation**
   ```
   - Process file with multiple stores
   - Query balance by store
   - Verify correct calculations
   ```

### Accepted Limitations

- Success scenarios only
- Does not cover all error cases
- Focus on validating complete functional flow

## Quality Gates

### Build Fails on Test Failure

- **Configuration**: Build fails if any test fails
- **CI/CD**: Pipeline stops on failure
- **Pre-commit**: Optional hooks to run tests

### Code Coverage

- **Minimum**: 70% overall coverage
- **Domain**: > 80% coverage
- **Application**: > 75% coverage
- **Tool**: Coverlet for .NET

### Static Analysis

- **Code Analysis**: .NET code analysis
- **Linting**: ESLint for frontend
- **Security Scanning**: Vulnerability checks

### Performance

- **Limits**: Tests must complete in reasonable time
- **Timeout**: Timeout configuration for tests
- **Isolation**: Tests must not interfere with each other

## Test Examples

### Unit Test (Domain)

```csharp
[Fact]
public void Transaction_Should_Calculate_Correct_Balance()
{
    // Arrange
    var store = new Store("MERCADO DA AVENIDA", "João Silva");
    var transaction1 = new Transaction(TransactionType.Debit, 50.00m, store);
    var transaction2 = new Transaction(TransactionType.Credit, 100.00m, store);
    
    // Act
    var balance = store.CalculateBalance();
    
    // Assert
    balance.Should().Be(50.00m);
}
```

### Integration Test (Database)

```csharp
[Fact]
public async Task Repository_Should_Persist_Transaction()
{
    // Arrange
    using var container = new TestcontainersBuilder<PostgreSqlTestcontainer>()
        .WithDatabase(new PostgreSqlTestcontainerConfiguration())
        .Build();
    await container.StartAsync();
    
    var context = new ApplicationDbContext(container.ConnectionString);
    var repository = new TransactionRepository(context);
    var transaction = new Transaction(...);
    
    // Act
    await repository.AddAsync(transaction);
    await context.SaveChangesAsync();
    
    // Assert
    var persisted = await repository.GetByIdAsync(transaction.Id);
    persisted.Should().NotBeNull();
}
```

### E2E Test (Happy Path)

```csharp
[Fact]
public async Task EndToEnd_Upload_Process_Query()
{
    // Arrange
    var fileContent = GetValidCnabFile();
    
    // Act - Upload
    var uploadResponse = await _client.PostAsync("/api/files/v1/upload", fileContent);
    uploadResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
    
    // Act - Wait for processing
    var fileId = await GetFileIdFromResponse(uploadResponse);
    await WaitForProcessing(fileId);
    
    // Act - Query
    var transactionsResponse = await _client.GetAsync("/api/transactions/v1");
    
    // Assert
    transactionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    var transactions = await transactionsResponse.Content.ReadAsAsync<List<TransactionDto>>();
    transactions.Should().NotBeEmpty();
}
```

## Test Execution

### Local

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter Category=Unit

# Integration tests only
dotnet test --filter Category=Integration

# With coverage
dotnet test /p:CollectCoverage=true
```

### CI/CD

- Automatic execution on each commit
- Coverage report
- Build failure on error
- Failure notifications

## Metrics and Reports

### Collected Metrics

- Test success rate
- Code coverage
- Execution time
- Failing tests

### Reports

- Coverage report (HTML)
- Test report (JUnit XML)
- Metrics dashboard (if configured)

## Test Maintenance

### Best Practices

- Tests should be fast and isolated
- Use realistic test data
- Avoid dependencies between tests
- Keep tests updated with code
- Refactor tests when necessary

### Anti-patterns to Avoid

- Fragile tests (implementation-dependent)
- Slow tests (unnecessarily)
- Overly complex tests
- Excessive mocking
- Tests that test frameworks

## Conclusion

The testing strategy ensures system quality and reliability, focusing on unit and integration tests, with E2E tests limited to the happy path to avoid excessive complexity. Quality gates ensure code meets standards before being integrated.
