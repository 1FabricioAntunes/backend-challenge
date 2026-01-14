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
