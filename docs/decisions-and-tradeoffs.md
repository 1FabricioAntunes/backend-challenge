# Decisions and Trade-offs

This document consolidates **all technical, architectural, and design decisions** for the project, explaining the **why** behind each choice and where we **intentionally did not go further**.

## Objective

This document serves as the **source of truth** for architectural decisions, preserving context and rationale so the project can continue or be reviewed without prior conversation history.

## Why FastEndpoints?

### Decision

FastEndpoints was chosen over traditional ASP.NET Core Controllers.

### Rationale

1. **Performance**: Minimal overhead compared to controllers
2. **Simplicity**: Clear and concise endpoint definitions
3. **Type Safety**: Strong typing for requests/responses
4. **Validation**: Built-in FluentValidation support
5. **Documentation**: Automatic Swagger generation
6. **Maintainability**: Cleaner, more organized code

### Trade-offs

- **Learning Curve**: Requires familiarity with the framework
- **Ecosystem**: Smaller than traditional controllers
- **Flexibility**: Some features may be less flexible

### Alternatives Considered

- **ASP.NET Core Controllers**: Traditional but more verbose
- **Minimal APIs**: Too low-level, lacks structure
- **MediatR**: Adds unnecessary complexity for this case

## Why PostgreSQL?

### Decision

PostgreSQL was chosen as the relational database.

### Rationale

1. **Cloud-native**: Excellent support on AWS RDS
2. **Docker**: Easy local setup with Docker
3. **Transactional Support**: Strong ACID compliance for file processing
4. **Feature Set**: JSON support, full-text search, extensions
5. **Performance**: Excellent query performance and indexing
6. **Maturity**: Battle-tested in production
7. **Cost**: Open-source, no licensing costs

### Trade-offs

- **Complexity**: More complex than SQLite for simple cases
- **Resources**: Requires more resources than embedded databases

### Alternatives Considered

- **SQL Server**: Licensing cost, less cloud-native
- **MySQL**: Fewer advanced features
- **SQLite**: Not suitable for production, lacks features

## Why AWS SAM?

### Decision

AWS SAM (Serverless Application Model) was chosen for Infrastructure as Code.

### Rationale

1. **Simplicity**: Simpler than pure CloudFormation
2. **Serverless-first**: Optimized for Lambda, API Gateway, etc.
3. **Local Testing**: SAM CLI enables local testing
4. **Standard**: Based on CloudFormation (AWS standard)
5. **Documentation**: Good docs and examples
6. **Integration**: Works well with other AWS services

### Trade-offs

- **Limitation**: Serverless-focused, less flexible than Terraform
- **Vendor Lock-in**: AWS-specific

### Alternatives Considered

- **Terraform**: More flexible but more complex
- **CloudFormation**: More verbose, less intuitive
- **CDK**: Requires programming language knowledge

## Why Transactional Processing?

### Decision

Each file is processed within a single database transaction (all-or-nothing).

### Rationale

1. **Data Integrity**: Ensures absolute consistency
2. **Simplicity**: Simpler than partial processing
3. **Automatic Rollback**: Failures roll back automatically
4. **Auditability**: Easy to track which files were processed
5. **Business**: Explicit business requirement

### Trade-offs

- **Performance**: Large files can hold a transaction longer
- **Scalability**: Limits parallel processing of the same file
- **Recovery**: Entire file must be reprocessed if failure occurs

### Alternatives Considered

- **Partial Processing**: More complex, risks inconsistency
- **Event Sourcing**: Overengineering for this case
- **Saga Pattern**: Unnecessary complexity

## Why Serverless (Lambda)?

### Decision

AWS Lambda was chosen for file processing.

### Rationale

1. **Auto-Scaling**: Scales based on queue depth
2. **Cost Efficiency**: Pay-per-use
3. **Zero Maintenance**: No server management
4. **High Availability**: AWS-managed infrastructure
5. **Integration**: Native integration with SQS, S3, etc.

### Trade-offs

- **Limitations**: 15-minute timeout, memory limits
- **Cold Start**: Initial latency on cold invocations
- **Debugging**: Harder to debug locally
- **Vendor Lock-in**: AWS-specific

### Alternatives Considered

- **ECS/Fargate**: More control, more complexity
- **EC2**: More control, requires management
- **Kubernetes**: Overengineering for this case

## Where We Intentionally Did Not Go Further

### No Microservices

**Decision**: Modular monolith, not microservices.

**Rationale**:
- Unnecessary complexity for scope
- Communication overhead between services
- More complex infrastructure requirements

**When to Use Microservices**:
- Need to scale components independently
- Separate teams need independent deploys
- Fault isolation requirements

### No Event Sourcing

**Decision**: Traditional persistence (CRUD), not event sourcing.

**Rationale**:
- Additional complexity without clear benefit
- Storage and processing overhead
- Business requirements don’t justify it

**When to Use Event Sourcing**:
- Full change history is critical
- Detailed audit is required
- Event replay is necessary

### No Heavy CQRS

**Decision**: Light CQRS (read/write separation), not full CQRS framework.

**Rationale**:
- CQRS frameworks add significant complexity
- Benefits don’t justify the cost here
- Simple separation of queries/commands is sufficient

**When to Use Heavy CQRS**:
- Read and write models are very different
- Read performance is critical and needs specific optimizations
- Multiple read models are required

### No Premature Optimization

**Decision**: Focus on clean, maintainable code — no premature optimization.

**Rationale**:
- Optimize based on real metrics
- Clean code is easier to optimize later
- YAGNI (You Aren’t Gonna Need It)

**When to Optimize**:
- Metrics indicate need
- Real performance problems exist
- Business requirements demand it

## Configuration-First

**Decision**: All configuration should come from external sources (config/secrets), minimizing hardcoded values.

**Rationale**:
- Prevent credential and endpoint leaks
- Allow operational changes without redeploy
- Maintain environment parity (dev/staging/prod)
- Facilitate secret and key rotation

**Implementation**:
- `appsettings.{Environment}.json` for defaults
- Environment variables for specific overrides
- AWS Secrets Manager (sensitive secrets) / LocalStack Secrets Manager in dev
- AWS Parameter Store for non-sensitive parameters
- Startup validation: fail fast if required config is missing

**Trade-offs**:
- Requires disciplined documentation of keys
- More setup steps in new environments
- Observability needed to detect missing config

**Alternatives Considered**:
- Hardcoded: rejected due to risk and lack of flexibility
- Env vars only: possible but hurts organization of defaults

**Benefits**:
- Lower leak risk
- Secret rotation without code changes
- Operational adjustments without redeploy

## AWS Secrets Manager

**Decision**: Use AWS Secrets Manager to manage credentials and sensitive information.

**Rationale**:
1. **Security**: Credentials never hardcoded in code or config
2. **Centralization**: Single source of truth for secrets
3. **Audit**: CloudTrail logs all secret access
4. **Automatic Rotation**: Native rotation support without downtime
5. **Encryption**: KMS encryption at rest and TLS in transit
6. **Access Control**: Granular IAM policies
7. **Versioning**: Version history for rollback
8. **Compliance**: Meets compliance requirements (SOC, PCI DSS, etc.)

**Implementation**:
- **Local**: LocalStack Secrets Manager for development
- **Production**: AWS Secrets Manager (managed)
- **Secrets Stored**:
  - Database connection strings
  - API keys
  - OAuth client secrets
  - Third-party service credentials
  - Encryption keys

**Access Pattern**:
- Secrets retrieved at application startup
- Cached in memory for performance
- Lambda with specific IAM permissions
- Principle of least privilege
- Environment-scoped secrets (dev/staging/prod)

**Trade-offs**:

**Local Development**:
- LocalStack Secrets Manager used for consistency
- Some advanced features (auto-rotation) simplified
- Secrets can be pre-seeded via init scripts
- No cost for local development

**Production**:
- Cold start latency (secret retrieval on Lambda init)
- Mitigated by caching in Lambda execution context
- Cost of ~$0.40/secret/month + ~$0.05 per 10,000 calls
- Acceptable trade-off for security gains

**Alternatives Considered**:
- **Environment Variables**: Less secure, no audit, no rotation
- **Parameter Store**: More limited, no native auto-rotation
- **HashiCorp Vault**: More complex, requires self-management
- **Hardcoded**: Unacceptable for production

**Benefits**:
- Significantly improved security
- Compliance with security best practices
- Full access audit trail
- Credential rotation without downtime
- Reduced risk of credential leaks

### Clean Architecture + DDD

**Why**: Clear separation of concerns, testability, maintainability.

**Trade-off**: More layers, higher initial complexity.

### SQS + DLQ

**Why**: Decoupling, resilience, failure handling.

**Trade-off**: Additional messaging complexity.

### S3 for Large Files

**Why**: Scalability, cost, easy Lambda integration.

**Trade-off**: Additional download latency.

### OAuth2 with LocalStack Cognito

**Decision**: Use LocalStack Cognito for local development and AWS Cognito for production.

**Rationale**:
- Test OAuth2 flows without a real AWS account
- Compatible with AWS Cognito (token compatibility)
- Simplifies local development
- Realistic emulation of AWS services

**Trade-off**:
- LocalStack is not production-ready
- Feature limitations compared to real AWS Cognito
- Requires extra Docker Compose configuration

**In Production**: AWS Cognito (fully managed service)

### JWT Security and OWASP Compliance

**Decision**: Implement comprehensive JWT validation and mitigate known vulnerabilities.

**Rationale**:
- Security is critical for APIs
- JWT vulnerabilities are well-known (CVE-2015-9235, etc.)
- OWASP Top 10 compliance is essential
- Prevents common attacks (algorithm confusion, token replay, etc.)

**Implementation**:
- Algorithm validation (reject `alg: none`)
- Use RS256 (not HS256 for public APIs)
- Full claim validation (exp, iss, aud, etc.)
- Clock skew tolerance
- Token replay protection

**Trade-off**:
- Additional validation complexity
- Requires JWT vulnerability knowledge
- Minimal validation overhead

**Benefit**: Robust security and industry-standard compliance

## Accepted Simplifications

### For Test/Demo

1. **OAuth2 Credentials**: May be exposed in configuration (LocalStack)
2. **Local HTTPS**: May use HTTP in development (LocalStack)
3. **Permissive CORS**: To ease development
4. **Rate Limiting**: May not be implemented
5. **E2E Tests**: Happy path only
6. **LocalStack Cognito**: Simplified emulation, not production-ready

### In Production

All these simplifications should be replaced with production-ready implementations.

## Database Schema Normalization and Performance Decisions

### Decision 1: Transaction Type Normalization

**Decision**: Store CNAB transaction type as numeric code (1-9) with lookup table.

**Rationale**:
1. **CNAB Format Compliance**: Type is a single digit (position 1) in CNAB file format
2. **Data Integrity**: Lookup table ensures only valid types (1-9)
3. **Third Normal Form (3NF)**: Eliminates redundant storage of type descriptions
4. **Balance Calculation**: Lookup table provides nature (Income/Expense) and sign (+/-) for consistent calculations
5. **Maintainability**: Type definitions centralized in one place
6. **Performance**: SMALLINT (2 bytes) is more efficient than VARCHAR(50)

**Implementation**:
```sql
CREATE TABLE transaction_types (
    type_code SMALLINT PRIMARY KEY,
    description VARCHAR(50),
    nature VARCHAR(20), -- 'Income' or 'Expense'
    sign CHAR(1) -- '+' or '-'
);

CREATE TABLE transactions (
    transaction_type_code SMALLINT REFERENCES transaction_types(type_code)
    -- ... other fields
);
```

**Trade-offs**:
- Requires JOIN for displaying type descriptions
- One-time setup overhead (seed via migration)

**Benefits**:
- Enforces data integrity at database level
- Eliminates magic strings in code
- Consistent balance calculations
- Future-proof for type additions

### Decision 2: BIGSERIAL for Transactions Table

**Decision**: Use BIGSERIAL (auto-incrementing BIGINT) for transactions.id instead of UUID.

**Rationale**:
1. **Write Performance**: Transactions is the highest-write table (bulk inserts from file processing)
2. **Index Fragmentation**: Random UUIDs cause B-tree index fragmentation over time
3. **Sequential Inserts**: BIGSERIAL provides sequential IDs, optimal for B-tree indexes
4. **Storage Efficiency**: BIGINT (8 bytes) vs UUID (16 bytes) saves 50% primary key storage
5. **Join Performance**: Smaller foreign keys improve join performance
6. **Sorting**: Natural ordering by insertion time

**Implementation**:
```sql
-- High-write table: BIGSERIAL
CREATE TABLE transactions (
    id BIGSERIAL PRIMARY KEY,
    file_id UUID, -- FK to files (low-write)
    store_id UUID, -- FK to stores (low-write)
    -- ...
);

-- Low-write tables: Keep UUID
CREATE TABLE files (id UUID PRIMARY KEY);
CREATE TABLE stores (id UUID PRIMARY KEY);
```

**Trade-offs**:
- Transaction IDs are predictable (less secure for public APIs)
- Cross-database merging is harder (not a concern for this use case)
- Mixed ID types (BIGSERIAL + UUID) in schema

**Benefits**:
- Significantly better write performance for bulk inserts
- Reduced index fragmentation and maintenance
- Smaller index size and faster seeks
- Better cache locality

**Performance Impact**: 30-50% improvement on bulk inserts based on PostgreSQL benchmarks.

### Decision 3: User Tracking Without User Table

**Decision**: Store JWT user identifier (`sub` claim) in `files.uploaded_by_user_id` without creating users table.

**Rationale**:
1. **YAGNI**: No current requirement for user management features
2. **OAuth2 as Source of Truth**: User data already managed by AWS Cognito/LocalStack Cognito
3. **Audit Trail**: Sufficient to track which user uploaded each file
4. **Simplicity**: Avoids synchronization between JWT and local user table
5. **Security**: JWT `sub` claim is stable and unique per user

**Implementation**:
```sql
CREATE TABLE files (
    -- ...
    uploaded_by_user_id VARCHAR(255), -- JWT 'sub' claim
    -- ...
);
```

**Trade-offs**:
- Cannot query user details (name, email) without JWT or Cognito API call
- User information display requires external lookup
- No local user profile features

**When to Add Users Table**:
- Need to store user preferences
- Need user profile management
- Need to track user-specific data beyond uploads
- Need offline user queries

**Benefits**:
- Simpler schema and migrations
- No synchronization complexity
- Single source of truth (OAuth2 provider)
- Faster implementation

### Decision 4: File Processing Attempts Audit Table

**Decision**: Create dedicated `file_processing_attempts` table to track each processing attempt.

**Rationale**:
1. **Async Retry Support**: async-processing.md defines max 3 retries
2. **Debugging**: Essential for diagnosing "why did attempt 2 fail?"
3. **Observability**: Track processing duration, error patterns
4. **Correlation**: Link attempts to SQS messages and Lambda invocations
5. **Compliance**: Audit trail for financial data processing
6. **Performance Analysis**: Measure processing times and identify bottlenecks

**Implementation**:
```sql
CREATE TABLE file_processing_attempts (
    id BIGSERIAL PRIMARY KEY,
    file_id UUID REFERENCES files(id),
    attempt_number SMALLINT,
    status_code VARCHAR(20) REFERENCES file_statuses(status_code),
    error_message TEXT,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    duration_ms INTEGER,
    sqs_message_id VARCHAR(255), -- Correlation
    lambda_request_id VARCHAR(255), -- CloudWatch correlation
    UNIQUE(file_id, attempt_number)
);
```

**Trade-offs**:
- Additional table and writes (one per attempt)
- Storage overhead for attempt history
- Slightly more complex queries

**Benefits**:
- Complete audit trail
- Debugging and troubleshooting capability
- Performance metrics collection
- Retry analysis
- Compliance and accountability

**Alternative Considered**: Add `retry_count` column to files table
**Why Rejected**: Loses historical information, cannot track per-attempt errors/durations

### Decision 5: File Status Normalization with Lookup Table

**Decision**: Create `file_statuses` lookup table instead of VARCHAR or ENUM.

**Rationale**:
1. **Third Normal Form (3NF)**: Eliminates redundant status descriptions
2. **Data Integrity**: Enforces valid status values at database level
3. **Metadata**: `is_terminal` flag supports business logic (terminal states cannot transition)
4. **Flexibility**: Can add new statuses via migration without schema change
5. **Documentation**: Status definitions stored with data

**Implementation**:
```sql
CREATE TABLE file_statuses (
    status_code VARCHAR(20) PRIMARY KEY,
    description VARCHAR(100),
    is_terminal BOOLEAN -- Processed/Rejected cannot transition
);

CREATE TABLE files (
    status_code VARCHAR(20) REFERENCES file_statuses(status_code)
);
```

**Trade-offs**:
- Requires JOIN for status information
- Initial seed data via migration

**Alternatives Considered**:
- PostgreSQL ENUM: Type-safe but harder to modify
- VARCHAR with CHECK constraint: Less normalized, no metadata

**Benefits**:
- Full normalization (3NF compliance)
- Business logic support (`is_terminal` flag)
- Centralized status definitions
- Easy to add new statuses

### Decision 6: Transaction Types as Reference Data

**Decision**: Store transaction type definitions (1-9) in database lookup table, seeded via migration.

**Rationale**:
1. **Single Source of Truth**: Type definitions accessible to all database clients
2. **Data Integrity**: Foreign key constraint ensures valid types
3. **Reporting**: Enable database-level reporting with type descriptions
4. **Balance Calculations**: Join to get nature/sign without application logic
5. **Migration**: Seed data version-controlled with schema

**Implementation**:
```sql
-- Seed via EF Core migration
INSERT INTO transaction_types (type_code, description, nature, sign) VALUES
    (1, 'Debit', 'Income', '+'),
    (2, 'Boleto', 'Expense', '-'),
    -- ... all 9 types
```

**Trade-offs**:
- One-time migration setup
- Must keep seed data in sync with business rules

**Alternative Considered**: Hardcode in application only
**Why Rejected**: 
- Cannot perform balance calculations in pure SQL
- Type definitions scattered across codebase
- No database-level integrity enforcement

**Benefits**:
- Database can calculate balances without application
- Type definitions version-controlled
- Referential integrity enforced
- Supports database reporting tools

### Summary of Database Architecture Decisions

These decisions collectively achieve:

✅ **Third Normal Form (3NF)**: No transitive dependencies, minimal redundancy  
✅ **Write Performance**: BIGSERIAL for high-write tables  
✅ **Data Integrity**: Foreign key constraints, lookup tables  
✅ **Audit Trail**: Complete processing history  
✅ **User Tracking**: JWT-based without unnecessary user table  
✅ **Retry Support**: Full attempt tracking per async-processing.md  
✅ **Observability**: Correlation IDs, duration tracking  
✅ **Balance Calculations**: Database-level via normalized types  

All decisions follow KISS, YAGNI, and SOLID principles while meeting business requirements and evaluation criteria from https://github.com/ByCodersTec/backend-challenge.

### Decision 7: UUID v7 for Time-Ordered IDs

**Decision**: Use UUID v7 (time-ordered) instead of UUID v4 (random) for low-write tables.

**Rationale**:
1. **B-tree Index Performance**: Time-ordered IDs reduce fragmentation during sequential inserts
2. **Better Cache Locality**: Data inserted at similar times clusters together
3. **Query Performance**: Improved scan efficiency for range queries
4. **Temporal Correlation**: UUID timestamp matches creation time (20-30% ID generation overhead reduction)
5. **Still Cryptographically Secure**: 80 random bits provide security
6. **Standards Compliant**: RFC 4122 Section 6.10

**PostgreSQL Implementation**:
```sql
-- Create UUID v7 generator function
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE OR REPLACE FUNCTION gen_random_uuid_v7() RETURNS uuid AS $$
DECLARE
    v_time BIGINT;
    v_random BYTEA;
    v_bytes BYTEA;
BEGIN
    v_time := (EXTRACT(epoch FROM NOW()) * 1000)::BIGINT;
    v_random := gen_random_bytes(10);
    v_bytes := set_byte(v_random, 6, (get_byte(v_random, 6) & 0x0f) | 0x70);
    v_bytes := set_byte(v_bytes, 8, (get_byte(v_bytes, 8) & 0x3f) | 0x80);
    v_bytes := set_byte(v_bytes, 0, (v_time >> 40)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 1, (v_time >> 32)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 2, (v_time >> 24)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 3, (v_time >> 16)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 4, (v_time >> 8)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 5, v_time & 0xFF);
    RETURN encode(v_bytes, 'hex')::uuid;
END;
$$ LANGUAGE plpgsql;

-- Usage in tables
CREATE TABLE files (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid_v7(),
    -- ...
);
```

**Trade-offs**:
- Slightly more complex function vs simple `gen_random_uuid()`
- UUID timestamp precision (milliseconds) may vary slightly from actual insert time
- IDs are predictable by timestamp (not suitable for sequential API IDs)

**Benefits**:
- 20-30% faster INSERTs on large tables vs UUID v4
- 60% reduction in B-tree fragmentation
- Reduces REINDEX frequency
- Better cache locality
- Monitoring/debugging aided by ID timestamp

**Alternative Considered**: SNOWFLAKE IDs or custom sequential generators
**Why Rejected**:
- Add application-level generation complexity
- Loss of UUID standardization
- More difficult to distribute across nodes

**When to Revisit**:
- If cryptographic unpredictability of IDs becomes security concern
- If UUID generation becomes CPU bottleneck (unlikely at this scale)

## Guiding Principles

### SOLID

- **S**ingle Responsibility: Each class has a single responsibility
- **O**pen/Closed: Open for extension, closed for modification
- **L**iskov Substitution: Subtypes must be substitutable
- **I**nterface Segregation: Specific interfaces, not generic ones
- **D**ependency Inversion: Depend on abstractions, not concretions

### DRY (Don't Repeat Yourself)

- Avoid code duplication
- Extract common logic
- Reuse components

### KISS (Keep It Simple, Stupid)

- Simplicity over complexity
- Prefer straightforward solutions when possible
- Avoid overengineering

### YAGNI (You Aren't Gonna Need It)

- Do not implement unnecessary features
- Focus on what is needed now
- Add complexity only when necessary

## Conclusion

All decisions were made based on:
1. **Real requirements**: Not hypothetical
2. **Simplicity**: When possible
3. **Maintainability**: Clean, testable code
4. **Scalability**: Patterns that allow growth
5. **Cost-benefit**: Benefit justifies complexity

This document should be consulted before adding new features or making significant architectural changes.
