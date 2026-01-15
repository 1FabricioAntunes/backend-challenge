# Backend

This document describes the backend implementation details, code organization, and technical decisions.

## Technology Stack

- **Language**: .NET (latest stable LTS)
- **Framework**: ASP.NET Core
- **API Framework**: FastEndpoints
- **Architecture**: Clean Architecture + DDD
- **ORM**: Entity Framework Core
- **Database**: PostgreSQL
- **Migrations**: EF Core Migrations
- **Authentication**: OAuth2
- **Documentation**: Swagger / OpenAPI (auto-generated)

## Code Organization

### Clean Architecture Layers

The backend follows Clean Architecture principles with clear layer separation:

```
Backend/
├── Domain/              # Core business logic
│   ├── Entities/        # Domain entities
│   ├── ValueObjects/    # Value objects
│   ├── Aggregates/      # Aggregate roots
│   └── Services/        # Domain services
├── Application/         # Use cases and orchestration
│   ├── UseCases/        # Application use cases
│   ├── DTOs/            # Data transfer objects
│   ├── Interfaces/      # Application interfaces
│   └── Validators/      # Input validators
├── Infrastructure/      # External concerns
│   ├── Persistence/     # EF Core context and repositories
│   ├── Messaging/       # SQS integration
│   ├── Storage/         # S3 integration
│   └── Authentication/   # OAuth2 implementation
└── Presentation/        # API layer
    ├── Endpoints/       # FastEndpoints
    ├── Middleware/      # Custom middleware
    └── Filters/         # Action filters
```

### Domain Layer

The domain layer contains:

- **Entities**: Core business entities (Store, Transaction, File)
- **Value Objects**: Immutable value objects (Money, DateRange)
- **Aggregates**: Aggregate roots with invariants
- **Domain Services**: Complex business logic that doesn't fit in entities

**Key Principle**: Domain layer has no dependencies on other layers.

### Application Layer

The application layer orchestrates use cases:

- **Use Cases**: Business operations (ProcessFile, GetTransactions)
- **DTOs**: Data transfer objects for API contracts
- **Interfaces**: Abstractions for infrastructure (repositories, services)
- **Validators**: Input validation logic

**Key Principle**: Application layer depends only on Domain layer.

### Infrastructure Layer

The infrastructure layer implements external concerns:

- **Persistence**: EF Core DbContext, repositories, migrations
- **Messaging**: SQS client integration
- **Storage**: S3 client integration
- **Authentication**: OAuth2 implementation

**Key Principle**: Infrastructure implements interfaces defined in Application layer.

### Presentation Layer

The presentation layer exposes APIs:

- **Endpoints**: FastEndpoints route handlers
- **Middleware**: Request/response processing
- **Filters**: Cross-cutting concerns

**Key Principle**: Presentation depends on Application layer.

## FastEndpoints

### Why FastEndpoints?

FastEndpoints was chosen for:

- **Performance**: Minimal overhead compared to controllers
- **Simplicity**: Clear endpoint definitions
- **Type Safety**: Strong typing for requests/responses
- **Validation**: Built-in validation support
- **Documentation**: Auto-generated Swagger support

### Endpoint Structure

```csharp
public class UploadFileEndpoint : Endpoint<UploadFileRequest, UploadFileResponse>
{
    public override void Configure()
    {
        Post("/api/files/v1/upload");
        AllowAnonymous(); // Or RequireAuthorization()
    }

    public override async Task HandleAsync(UploadFileRequest req, CancellationToken ct)
    {
        // Implementation
    }
}
```

### Endpoints
 
**API Versioning (URL-based, v1)**

- `POST /api/files/v1/upload` - Upload file metadata
- `GET /api/files/v1` - List uploaded files
- `GET /api/files/v1/{id}` - Get file status
- `GET /api/transactions/v1` - Query transactions (with filters)
- `GET /api/stores/v1` - List stores with balances

**Versioning Strategy**
- Major version in URL path (v1, v2, ...)
- Breaking changes require new major version
- Non-breaking changes do not bump version
- Minimum 6-month deprecation notice for old versions
- Document changes in changelog and Swagger description

**Query Parameters Example**
```
GET /api/transactions/v1?
    storeName=Store+A&        // validated & sanitized
    startDate=2026-01-01&     // ISO 8601 UTC
    endDate=2026-01-31&       // range-checked
    page=1&                   // pagination
    pageSize=50               // max limit enforced
```

## Validation

### Input Validation

- **FastEndpoints Validators**: FluentValidation integration
- **Request Validation**: Automatic validation before handler execution
- **Domain Validation**: Business rule validation in domain layer

### Validation Rules

- File metadata validation
- Query parameter validation
- Date range validation
- Store filter validation

### Input Sanitization

**Decision**: Explicit sanitization for all user inputs to prevent injection attacks

**Sanitization Rules**:

1. **String Fields**:
   ```csharp
   // Store name sanitization
   public class StoreNameValidator : AbstractValidator<string>
   {
       public StoreNameValidator()
       {
           RuleFor(x => x)
               .NotEmpty()
               .MinimumLength(1)
               .MaximumLength(100)  // ✅ Prevent buffer overflow
               .Matches(@"^[a-zA-Z0-9\s\-_]+$")  // ✅ Character whitelist
               .Must(NotContainSqlKeywords)  // ✅ SQL injection prevention
               .Must(NotContainHtmlTags);  // ✅ XSS prevention
       }
       
       private bool NotContainSqlKeywords(string value)
       {
           var sqlKeywords = new[] { "--", "/*", "*/", "xp_", "sp_", "DROP", "SELECT", "INSERT" };
           return !sqlKeywords.Any(k => value.Contains(k, StringComparison.OrdinalIgnoreCase));
       }
       
       private bool NotContainHtmlTags(string value)
       {
           return !value.Contains("<") && !value.Contains(">");
       }
   }
   ```

2. **Query Parameters**:
   - Max length enforcement
   - Type validation
   - Range validation
   - Whitelist of allowed values

3. **File Uploads**:
   - File size limit: 10MB max
   - File extension whitelist: .txt, .cnab only
   - Content-Type validation
   - Filename sanitization (remove special chars)
   - Virus scanning (production)

4. **Date Inputs**:
   - ISO 8601 format validation
   - Range validation (not in future, not too old)
   - Timezone handling (convert to UTC)

**SQL Injection Prevention**:
- ✅ Parameterized queries (EF Core default)
- ✅ No string concatenation in queries
- ✅ Input validation before database calls
- ✅ ORM-level protection

**XSS Prevention**:
- ✅ HTML encoding for all outputs
- ✅ Content Security Policy headers
- ✅ No eval() or innerHTML in frontend
- ✅ Sanitize before storing in database

## Error Handling

### Consistent Error Responses

All errors follow a consistent format:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "File format is invalid",
    "details": []
  }
}
```

### Error Types

- **Validation Errors**: 400 Bad Request
- **Not Found**: 404 Not Found
- **Processing Errors**: 500 Internal Server Error
- **Authentication Errors**: 401 Unauthorized

### Exception Handling

- Global exception handler middleware
- Structured error logging
- Correlation ID tracking
- User-friendly error messages

## Database Access

### Entity Framework Core

- **DbContext**: Main database context
- **Repositories**: Abstraction over EF Core
- **Unit of Work**: Transaction management
- **Migrations**: Schema versioning

### Repository Pattern

```csharp
public interface ITransactionRepository
{
    Task<Transaction> GetByIdAsync(Guid id);
    Task<IEnumerable<Transaction>> GetByStoreAsync(string storeName);
    Task AddAsync(Transaction transaction);
}
```

### Entity Framework Core Performance

**Critical Optimizations**:

1. **AsNoTracking() for Read Queries**:
```csharp
// ✅ CORRECT: Read-only queries don't need tracking
public async Task<List<TransactionDto>> GetAllAsync()
{
    return await _context.Transactions
        .AsNoTracking()  // ✅ 30-40% memory reduction
        .Include(t => t.Store)
        .Select(t => new TransactionDto  // ✅ Projection reduces data transfer
        {
            Id = t.Id,
            Amount = t.Amount,
            StoreName = t.Store.Name,
            Date = t.TransactionDate
        })
        .ToListAsync();
}

// ❌ WRONG: Tracking overhead for read-only data
public async Task<List<Transaction>> GetAllAsync()
{
    return await _context.Transactions  // ❌ Tracks entities unnecessarily
        .ToListAsync();
}
```

2. **Avoid N+1 Query Problem**:
```csharp
// ✅ CORRECT: Single query with Include
var transactions = await _context.Transactions
    .AsNoTracking()
    .Include(t => t.Store)  // ✅ Eager loading
    .ToListAsync();

// ❌ WRONG: N+1 queries (1 for transactions + N for stores)
var transactions = await _context.Transactions.ToListAsync();
foreach (var t in transactions)
{
    var store = await _context.Stores.FindAsync(t.StoreId);  // ❌ N additional queries
}
```

3. **Batch Operations**:
```csharp
// ✅ Batch insert for file processing
await _context.Transactions.AddRangeAsync(transactions);
await _context.SaveChangesAsync();  // Single round-trip
```

4. **Explicit Projections**:
```csharp
// ✅ Only select needed columns
var storeNames = await _context.Stores
    .AsNoTracking()
    .Select(s => s.Name)  // Only fetches Name column
    .ToListAsync();
```

**Performance Checklist**:
- ✅ All read-only queries use `.AsNoTracking()`
- ✅ Related entities loaded with `.Include()` (not lazy loading)
- ✅ Projections used to fetch only needed data
- ✅ Batch operations for multiple inserts/updates
- ✅ Indexes on frequently queried columns
- ✅ Query logging enabled for slow query detection

### Transaction Management

- Single transaction per file processing
- Explicit transaction boundaries
- Rollback on any error
- Idempotent operations

### DateTime Handling

**Standard**: ISO 8601 format with UTC timezone

**Format**: `yyyy-MM-ddTHH:mm:ss.fffZ`

**JSON Serialization Configuration**:
```csharp
services.AddControllers()
    .AddJsonOptions(options =>
    {
        // DateTime serialization
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter());
        
        // Naming policy
        options.JsonSerializerOptions.PropertyNamingPolicy = 
            JsonNamingPolicy.CamelCase;
        
        // ISO 8601 UTC format (default in .NET)
        // All DateTime values automatically serialized as:
        // "2026-01-13T14:30:00.000Z"
    });
```

**Database Storage**:
- PostgreSQL type: `timestamp with time zone`
- All dates stored as UTC
- No local timezone assumptions
- EF Core configuration:
```csharp
entity.Property(e => e.CreatedAt)
    .HasColumnType("timestamp with time zone")
    .HasDefaultValueSql("NOW()");
```

**API Consistency**:
- All request timestamps must be ISO 8601 UTC
- All response timestamps are ISO 8601 UTC
- Validation rejects invalid formats
- Client libraries parse timestamps correctly across all platforms

## Authentication

### OAuth2 Implementation

- **Token-based authentication**: JWT tokens for API access
- **Authorization Server**: LocalStack Cognito (local) / AWS Cognito (production)
- **JWT Token Validation**: Comprehensive validation including signature, expiration, issuer, audience
- **Authorization policies**: Role-based access control (RBAC)
- **Role-based access**: User roles from token claims

### LocalStack Cognito Integration

#### Configuration

```csharp
// LocalStack Cognito configuration
services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "http://localhost:4566"; // LocalStack endpoint
        options.RequireHttpsMetadata = false; // Local development only
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "http://localhost:4566/us-east-1_local",
            ValidateAudience = true,
            ValidAudience = "api-client-id",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true
        };
    });
```

#### Token Validation

Comprehensive JWT validation includes:

- **Signature Verification**: Validate signature using issuer's public key
- **Algorithm Validation**: Reject `alg: none` and unexpected algorithms
- **Expiration Check**: Validate `exp` claim with clock skew tolerance
- **Issuer Validation**: Verify `iss` claim matches expected issuer
- **Audience Validation**: Verify `aud` claim matches expected audience
- **Claims Validation**: Validate required claims (`sub`, `iat`, `nbf`)

### JWT Security Implementation

#### Algorithm Security

```csharp
// Prevent algorithm confusion attacks
options.TokenValidationParameters = new TokenValidationParameters
{
    // Explicitly require RS256
    AlgorithmValidator = (algorithm, securityKey, tokenValidationParameters) =>
    {
        if (algorithm != SecurityAlgorithms.RsaSha256)
        {
            throw new SecurityTokenInvalidAlgorithmException(
                $"Invalid algorithm: {algorithm}. Expected RS256.");
        }
        return true;
    },
    // Reject unsigned tokens
    RequireSignedTokens = true,
    // Validate signature
    ValidateIssuerSigningKey = true
};
```

#### Claims Validation

```csharp
// Validate all required claims
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    RequireExpirationTime = true,
    RequireSignedTokens = true,
    ClockSkew = TimeSpan.FromMinutes(5) // Clock skew tolerance
};
```

### Security Considerations

#### OWASP Compliance

- **A01: Broken Access Control**: Token validation on every request
- **A02: Cryptographic Failures**: Strong algorithms (RS256), HTTPS enforcement
- **A03: Injection**: Parameterized queries, input validation
- **A07: Identification Failures**: Token expiration, refresh tokens, secure storage

#### JWT Vulnerabilities Mitigation

- **Algorithm Confusion**: Explicit algorithm validation, reject `alg: none`
- **Weak Algorithms**: Use RS256, reject weak algorithms
- **Expiration Bypass**: Always validate expiration with clock skew
- **Token Replay**: Short-lived tokens, refresh mechanism
- **Insecure Storage**: HttpOnly cookies or secure memory storage
- **Missing Signature**: Always verify signature
- **Insufficient Claims**: Validate all required claims

### Token Storage

- **Frontend**: HttpOnly cookies (preferred) or secure memory storage
- **Never**: localStorage for sensitive applications
- **Logging**: Never log tokens, mask in error messages

### Production Considerations

- **AWS Cognito**: Replace LocalStack with AWS Cognito
- **HTTPS**: Enforce HTTPS everywhere
- **Token Refresh**: Implement refresh token flow
- **Token Revocation**: Consider revocation list for critical operations

**Note**: For test/demo purposes, LocalStack Cognito is used. See [Security](security.md) for production considerations and OWASP compliance details.

## API Documentation

### Swagger/OpenAPI

- Auto-generated from FastEndpoints
- Interactive API documentation
- Request/response schemas
- Authentication documentation

### Accessing Swagger

- Development: `https://localhost:5001/swagger`
- Production: Configured endpoint

## Logging

### Structured Logging

- Serilog or similar structured logging
- JSON log format
- Correlation IDs
- Context-rich entries

### Log Levels

- **Trace**: Detailed diagnostic information
- **Debug**: Debugging information
- **Info**: General information
- **Warn**: Warning messages
- **Error**: Error messages
- **Fatal**: Critical errors

## Configuration

### Configuration Sources

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- Environment variables - Production configuration
- AWS Secrets Manager - Sensitive data (primary)
- AWS Parameter Store - Non-sensitive cloud configuration

### Configuration-First Principle

- Never hardcode connection strings, URLs, timeouts, credentials, or limits
- Defaults live in appsettings; higher environments override via env vars/Secrets Manager
- Validate configuration at startup and fail fast on missing/invalid keys
- Prefer configuration keys for tunables (pagination limits, retries, timeouts)
- Document each key (purpose, example value, environment scope)

### AWS Secrets Manager Integration

#### Overview

The application uses AWS Secrets Manager to securely manage sensitive configuration:

**Local Development**:
- LocalStack Secrets Manager emulation
- Configured in Docker Compose
- Endpoint: `http://localhost:4566`
- Pre-seeded secrets via init scripts

**Production**:
- AWS Secrets Manager service
- Encryption at rest with AWS KMS
- Automatic secret rotation support
- IAM-based access control
- CloudTrail audit logging

#### Secrets Structure

```
TransactionProcessor/
├── Database/
│   └── ConnectionString          # PostgreSQL connection string
├── AWS/
│   ├── S3/
│   │   └── BucketName           # S3 bucket configuration
│   └── SQS/
│       └── QueueUrl             # SQS queue URLs
├── OAuth/
│   └── ClientSecret             # OAuth client secret (prod only)
└── JWT/
    └── SigningKey               # JWT signing key (if applicable)
```

#### Secret Retrieval

```csharp
// Retrieve secret at application startup
var secretsClient = new AmazonSecretsManagerClient(config);
var request = new GetSecretValueRequest
{
    SecretId = "TransactionProcessor/Database/ConnectionString"
};
var response = await secretsClient.GetSecretValueAsync(request);
var connectionString = response.SecretString;

// Cache in memory for performance
// Refresh on rotation notifications (production)
```

#### Security Benefits

- ✅ No hardcoded credentials in code or configuration files
- ✅ Centralized secrets management
- ✅ Automatic encryption at rest and in transit
- ✅ Audit trail via CloudTrail
- ✅ Secret rotation without application restart
- ✅ Version control for rollback capability
- ✅ Fine-grained IAM access control

### Key Configuration

**From Secrets Manager (Sensitive)**:
- Database connection strings
- OAuth2 client secrets
- API keys for third-party services
- Encryption keys

**From Parameter Store (Non-Sensitive)**:
- S3 bucket names
- SQS queue URLs
- Feature flags
- Application settings

**From appsettings.json (Non-Sensitive)**:
- Logging configuration
- CORS settings
- API endpoints (non-production)

## Testing

### Unit Tests

- Domain logic testing
- Use case testing
- Validator testing
- Mock external dependencies

### Integration Tests

- Database integration
- API endpoint testing
- End-to-end scenarios

See [Testing Strategy](testing-strategy.md) for details.

## Performance Considerations

### Optimization Strategies

- Connection pooling for database
- Async/await throughout
- Efficient queries (avoid N+1)
- Caching where appropriate
- Response compression

### Monitoring

- Request duration tracking
- Database query performance
- Memory usage monitoring
- Error rate tracking

See [Observability](observability.md) for details.

## Deployment

### Local Development

- `dotnet run` for API
- Docker Compose for dependencies
- Hot reload support

### Cloud Deployment

- AWS Lambda for serverless
- API Gateway for HTTP endpoints
- Environment-specific configuration
- Automated deployments

## Code Quality

### Standards

- SOLID principles
- Clean code practices
- Consistent naming conventions
- Comprehensive comments
- XML documentation

### Tools

- Code analysis
- Linting
- Formatting
- Pre-commit hooks
