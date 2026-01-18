# Database

This document describes the database design, schema, and data persistence strategy.

## Database Choice

### PostgreSQL

PostgreSQL was chosen for:

- **Cloud-native friendliness**: Excellent support on AWS RDS
- **Docker compatibility**: Easy local development setup
- **Strong transactional support**: ACID compliance for file processing
- **Rich feature set**: JSON support, full-text search, extensions
- **Performance**: Excellent query performance and indexing
- **Maturity**: Battle-tested in production environments

## Database Schema

### Design Principles

**Normalization**: Schema follows Third Normal Form (3NF)

- Transaction types normalized to lookup table
- File statuses normalized to lookup table
- No transitive dependencies
- Minimal data redundancy

**Performance**: Optimized for write-heavy workloads

- **UUID v7** for low-write tables (time-ordered, reduces B-tree fragmentation)
- BIGSERIAL for high-write tables (transactions)
- Strategic indexing on query patterns
- Batch insert optimization

**Audit Trail**: Track processing history

- File processing attempts table
- User identification from JWT token
- Timestamp tracking throughout

### UUID v7 Configuration

**Choice**: PostgreSQL UUID v7 (time-ordered, microsecond precision)

**Why UUID v7 over v4**:

- **Time-ordered**: First 48 bits contain timestamp, natural sorting
- **B-tree friendly**: Sequential inserts reduce index fragmentation
- **Performance**: Better INSERT performance vs random UUIDs
- **Observability**: ID timestamp correlates with creation time
- **Still secure**: Random bits (80) provide cryptographic randomness

**PostgreSQL Implementation**:

UUID v7 is available natively in PostgreSQL 13.10+ via `gen_random_uuid()` with proper configuration, or via `pgcrypto` extension for older versions.

```sql
-- For PostgreSQL 13.10+ (native support)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Function to generate UUID v7
CREATE OR REPLACE FUNCTION gen_random_uuid_v7() RETURNS uuid AS $$
DECLARE
    v_time BIGINT;
    v_random BYTEA;
    v_bytes BYTEA;
BEGIN
    -- Get current timestamp in milliseconds
    v_time := (EXTRACT(epoch FROM NOW()) * 1000)::BIGINT;
    -- Generate random bytes (80 bits for randomness)
    v_random := gen_random_bytes(10);
    -- Construct UUID v7: 48-bit timestamp + 4-bit version + 12-bit random + 2-bit variant + 62-bit random
    v_bytes := set_byte(v_random, 6, (get_byte(v_random, 6) & 0x0f) | 0x70);
    v_bytes := set_byte(v_bytes, 8, (get_byte(v_bytes, 8) & 0x3f) | 0x80);
    -- Pack timestamp into first 6 bytes
    v_bytes := set_byte(v_bytes, 0, (v_time >> 40)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 1, (v_time >> 32)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 2, (v_time >> 24)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 3, (v_time >> 16)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 4, (v_time >> 8)::int & 0xFF);
    v_bytes := set_byte(v_bytes, 5, v_time & 0xFF);
    -- Convert to UUID
    RETURN encode(v_bytes, 'hex')::uuid;
END;
$$ LANGUAGE plpgsql;
```

**Alternative (PostgreSQL 13+)**: Use third-party extension

```bash
# Install uuid-ossp or pgvector extension with uuid7 support
CREATE EXTENSION IF NOT EXISTS uuid_ossp;
```

**Usage in Migrations**: Seed function via EF Core migration before applying schema:

```csharp
// In migration
migrationBuilder.Sql(@"
    CREATE OR REPLACE FUNCTION gen_random_uuid_v7() ...
");
```

## Implemented Schema (Target Design)

### Overview

This documentation defines the target, normalized schema to be implemented. It aligns with the CNAB challenge fields and removes persisted calculated values.

### Entity Relationship Diagram

```mermaid
erDiagram
    TRANSACTION_TYPES ||--o{ TRANSACTIONS : defines
    FILE_STATUSES ||--o{ FILES : has
    FILES ||--o{ FILE_PROCESSING_ATTEMPTS : tracks
    FILES ||--o{ TRANSACTIONS : contains
    STORES ||--o{ TRANSACTIONS : has
    
    TRANSACTION_TYPES {
        smallint TypeCode PK
        varchar Description
        varchar Nature "Income|Expense"
        char Sign "+|-"
    }

    FILE_STATUSES {
        varchar StatusCode PK
        varchar Description
        boolean IsTerminal
    }

    FILES {
        uuid Id PK
        varchar FileName "max 255"
        bigint FileSize
        varchar S3Key UK "max 500"
        varchar StatusCode FK
        varchar UploadedByUserId "nullable"
        text ErrorMessage "nullable"
        timestamptz UploadedAt
        timestamptz ProcessedAt "nullable"
        timestamptz CreatedAt
        timestamptz UpdatedAt
    }

    STORES {
        uuid Id PK
        varchar Name "max 19"
        varchar OwnerName "max 14"
        timestamptz CreatedAt
        timestamptz UpdatedAt
        unique Name_OwnerName "(Name, OwnerName)"
    }

    TRANSACTIONS {
        bigserial Id PK
        uuid FileId FK
        uuid StoreId FK
        smallint TransactionTypeCode FK
        numeric Amount "18,2 precision"
        varchar CPF "max 11"
        varchar CardNumber "max 12"
        date TransactionDate
        time TransactionTime
        timestamptz CreatedAt
        timestamptz UpdatedAt
    }

    FILE_PROCESSING_ATTEMPTS {
        bigserial Id PK
        uuid FileId FK
        smallint AttemptNumber
        varchar StatusCode FK
        text ErrorMessage "nullable"
        timestamptz StartedAt
        timestamptz CompletedAt "nullable"
        integer DurationMs "nullable"
        varchar SqsMessageId "nullable"
        varchar WorkerRequestId "nullable"
    }
```

**Relationships**:

- Files → Transactions: One-to-Many, CASCADE delete
- Stores → Transactions: One-to-Many, RESTRICT delete
- TransactionTypes → Transactions: One-to-Many
- FileStatuses → Files: One-to-Many
- Files → FileProcessingAttempts: One-to-Many

**Key Design Decisions**:

- Aggregations (e.g., store totals) are computed from transactions; not persisted.
- Normalize status and type for integrity and auditability.
- Use BIGSERIAL for high-write `Transactions.Id` to reduce B-tree fragmentation.

### Files Table

Stores information about uploaded CNAB files and processing status.

```sql
CREATE TABLE "Files" (
    "Id" UUID PRIMARY KEY,
    "FileName" VARCHAR(255) NOT NULL,
    "FileSize" BIGINT NOT NULL,
    "S3Key" VARCHAR(500) NOT NULL UNIQUE,
    "StatusCode" VARCHAR(50) NOT NULL DEFAULT 'Uploaded' REFERENCES "file_statuses"("status_code"),
    "UploadedByUserId" UUID,
    "ErrorMessage" VARCHAR(1000),
    "UploadedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "ProcessedAt" TIMESTAMP WITH TIME ZONE
);

CREATE INDEX "idx_files_status_code" ON "Files"("StatusCode");
CREATE INDEX "idx_files_uploaded_at" ON "Files"("UploadedAt");
CREATE INDEX "idx_files_uploaded_by_user" ON "Files"("UploadedByUserId");
```

**Column Details**:

- `Id`: UUID (time-ordered via UUID v7), primary key
- `FileName`: String (max 255), original file name
- `FileSize`: Long (bigint), file size in bytes
- `S3Key`: String (max 500), S3 object key for file retrieval, unique constraint
- `StatusCode`: String (max 50), foreign key to file_statuses, default 'Uploaded'
- `UploadedByUserId`: UUID (nullable), user ID from JWT token
- `ErrorMessage`: String (max 1000, nullable), validation/processing error details
- `UploadedAt`: DateTime with timezone, UTC timestamp of upload
- `ProcessedAt`: DateTime with timezone (nullable), set when processing completes

### Stores Table

Stores extracted from CNAB transactions. Aggregations are computed on demand.

```sql
CREATE TABLE "Stores" (
    "Id" UUID PRIMARY KEY,
    "Name" VARCHAR(19) NOT NULL,
    "owner_name" VARCHAR(14) NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    CONSTRAINT "idx_stores_name_owner_unique" UNIQUE ("Name", "owner_name")
);
```

**Column Details**:

- `Id`: UUID (time-ordered via UUID v7), primary key
- `Name`: String (max 19), store name from CNAB header
- `owner_name`: String (max 14), store owner/manager name
- `CreatedAt`: DateTime with timezone, UTC timestamp of first appearance
- `UpdatedAt`: DateTime with timezone, UTC timestamp of last update
- **Composite Unique Index**: (Name, OwnerName) prevents duplicate store entries with same identity

### Transactions Table

Stores individual transaction records from CNAB files. Uses BIGSERIAL for high-write performance optimization.

```sql
CREATE TABLE "Transactions" (
    "Id" BIGSERIAL PRIMARY KEY,
    "FileId" UUID NOT NULL REFERENCES "Files"("Id") ON DELETE CASCADE,
    "StoreId" UUID NOT NULL REFERENCES "Stores"("Id") ON DELETE RESTRICT,
    "transaction_type_code" VARCHAR(10) NOT NULL REFERENCES "transaction_types"("type_code"),
    "Amount" NUMERIC(18, 2) NOT NULL,
    "CPF" VARCHAR(11) NOT NULL,
    "Card" VARCHAR(12) NOT NULL,
    "transaction_date" DATE NOT NULL,
    "transaction_time" TIME NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL
);

CREATE INDEX "idx_transactions_file_id" ON "Transactions"("FileId");
CREATE INDEX "idx_transactions_store_id" ON "Transactions"("StoreId");
CREATE INDEX "idx_transactions_date" ON "Transactions"("transaction_date");
CREATE INDEX "idx_transactions_store_date" ON "Transactions"("StoreId", "transaction_date");
```

**Column Details**:

- `Id`: Long (BIGSERIAL), auto-incrementing primary key optimized for high-write volume
- `FileId`: UUID, foreign key to Files, CASCADE delete (orphaned transactions removed)
- `StoreId`: UUID, foreign key to Stores, RESTRICT delete (prevents accidental store deletion)
- `transaction_type_code`: String (max 10), foreign key to transaction_types (codes 1-9)
- `Amount`: Decimal (18,2), transaction amount in BRL (always positive, sign determined by type)
- `CPF`: String (max 11), customer CPF (11 digits for Brazilian taxpayer ID)
- `Card`: String (max 12), card number (last 12 digits from CNAB)
- `transaction_date`: Date, transaction occurrence date
- `transaction_time`: Time, transaction occurrence time
- `CreatedAt`: DateTime with timezone, UTC timestamp of record creation
- `UpdatedAt`: DateTime with timezone, UTC timestamp of last update

**BIGSERIAL Rationale**: Using BIGSERIAL (long) instead of UUID for this high-write table provides:

- Sequential inserts reduce B-tree fragmentation
- Better cache locality in index pages
- Faster inserts compared to random UUID v4
- Still supports distributed tracing via FileId relationship

### Data Types and Constraints

**Primary Key Strategy**:

- **UUID for Files and Stores**: Distributed uniqueness, time-ordered via UUID v7 function
  - Reduces B-tree index fragmentation
  - Natural ordering by creation timestamp
  - Supports global distributed unique IDs
- **BIGSERIAL for Transactions**: Optimizes high-write performance
  - Sequential auto-increment reduces index page splits
  - Better cache locality in B-tree
  - Significantly faster bulk inserts during file processing
  - Can insert 10,000+ transactions/second

**Decimal Precision**:

- `NUMERIC(18, 2)` for all monetary values (Amount, Balance if persisted)
- 18 total digits, 2 decimal places
- Prevents floating-point precision errors (critical for financial data)
- Example: 999,999,999,999,999.99 BRL maximum value

**Timestamp Strategy**:

- All timestamps stored as `TIMESTAMP WITH TIME ZONE`
- PostgreSQL interprets as UTC internally regardless of session timezone
- Application layer converts all DateTime to UTC before persistence
- Ensures consistent sorting and comparison across timezones
- Client applications handle timezone conversion for display

**String Lengths** (match CNAB 240 specification):

- `FileName`: 255 characters (standard file system limit)
- `Name`: 19 characters (CNAB store name field)
- `owner_name`: 14 characters (CNAB store owner field)
- `CPF`: 11 characters (Brazilian CPF format: 11 digits)
- `Card`: 12 characters (CNAB card number field)
- `transaction_type_code`: 10 characters (codes "1" through "9")
- `StatusCode`: 50 characters (enum values: Uploaded, Processing, Processed, Rejected)
- `S3Key`: 500 characters (S3 object key pattern: `cnab/{fileId}/{filename}`)
- `ErrorMessage`: 1000 characters (detailed validation/processing error messages)

**Nullability**:

- `ProcessedAt`: Nullable (NULL until file processing completes)
- `UploadedByUserId`: Nullable (may be anonymous upload)
- `ErrorMessage`: Nullable (NULL unless validation/processing failed)
- All other fields: NOT NULL (required for data integrity)

### Relationships and Referential Integrity

**One-to-Many Relationships**:

1. **Files → Transactions** (Cascade Delete):

   ```sql
   ALTER TABLE "Transactions" 
   ADD CONSTRAINT "fk_transactions_files" 
       FOREIGN KEY ("FileId") REFERENCES "Files"("Id") ON DELETE CASCADE;
   ```

   - One file contains many transactions
   - When file is deleted, all related transactions are automatically deleted
   - Ensures data integrity: no orphaned transactions
   - Safe cleanup during file archival or error recovery

2. **Stores → Transactions** (Restrict Delete):

   ```sql
   ALTER TABLE "Transactions" 
   ADD CONSTRAINT "fk_transactions_stores" 
       FOREIGN KEY ("StoreId") REFERENCES "Stores"("Id") ON DELETE RESTRICT;
   ```

   - One store has many transactions
   - Store cannot be deleted if it has transactions
   - Protects historical transaction data
   - Prevents accidental loss of audit trail

3. **TransactionTypes → Transactions** (Restrict Delete):

   ```sql
   ALTER TABLE "Transactions" 
   ADD CONSTRAINT "fk_transactions_types" 
       FOREIGN KEY ("transaction_type_code") REFERENCES "transaction_types"("type_code") ON DELETE RESTRICT;
   ```

   - Transaction type (code 1-9) is immutable
   - Prevents deletion of referenced type codes

4. **FileStatuses → Files** (Restrict Delete):

   ```sql
   ALTER TABLE "Files" 
   ADD CONSTRAINT "fk_files_statuses" 
       FOREIGN KEY ("StatusCode") REFERENCES "file_statuses"("status_code") ON DELETE RESTRICT;
   ```

   - File status is immutable (cannot delete status in use)
   - Prevents data corruption of file state

**Foreign Key Enforcement**:

- Database-level constraints ensure referential integrity at storage layer
- Invalid FileId or StoreId values rejected by PostgreSQL
- Transaction isolation (SERIALIZABLE level) prevents race conditions during inserts
- All constraints enforced before transaction commits

### CNAB Transaction Type Reference

Transaction types (lookup table) follow CNAB specifications:

| Type | Description       | Nature  | Sign | Balance Impact |
|------|-------------------|---------|------|----------------|
| 1    | Débito            | Income  | +    | Credit         |
| 2    | Boleto            | Expense | -    | Debit          |
| 3    | Financiamento     | Expense | -    | Debit          |
| 4    | Crédito           | Income  | +    | Credit         |
| 5    | Recebimento Empr. | Income  | +    | Credit         |
| 6    | Vendas            | Income  | +    | Credit         |
| 7    | Recebimento TED   | Income  | +    | Credit         |
| 8    | Recebimento DOC   | Income  | +    | Credit         |
| 9    | Aluguel           | Expense | -    | Debit          |

**Implementation**: Signed amount calculation is derived from `sign` in `transaction_types` and applied in queries/services.

### File Status Reference

Status values are stored in `file_statuses` and referenced by `files.status_code`:

| Status     | Description                              | Terminal | Transitions            |
|------------|------------------------------------------|----------|------------------------|
| Uploaded   | File uploaded, awaiting processing       | No       | → Processing           |
| Processing | File currently being processed           | No       | → Processed, Rejected  |
| Processed  | File successfully processed              | Yes      | (final state)          |
| Rejected   | File rejected due to validation/errors   | Yes      | (final state)          |

**Implementation**: Transitions enforced by application logic and audited in `file_processing_attempts`.

## Data Integrity and Constraints

### Primary Keys

- All tables use UUID (Guid) primary keys
- Generated by application layer via `Guid.NewGuid()`
- Ensures global uniqueness across distributed systems

### Foreign Keys

- **Transactions.FileId → Files.Id**: CASCADE delete behavior
  - Ensures no orphaned transactions when file is deleted
  - Critical for data cleanup and consistency
- **Transactions.StoreId → Stores.Id**: RESTRICT delete behavior
  - Prevents deletion of stores with transaction history
  - Protects historical data integrity

### Unique Constraints

- **Stores.Code**: Business-level unique identifier
  - CNAB store codes must be unique system-wide
  - Supports efficient upsert operations during processing
  - Enforced by unique index `IX_Stores_Code`

### Data Type Constraints

- **Monetary Values**: NUMERIC(18, 2) prevents floating-point errors
- **Timestamps**: All timestamps stored with timezone (UTC)
- **String Lengths**: Match CNAB field specifications (Code: 14, Name: 19, CPF: 11, Card: 12)
- **Nullability**: Only ProcessedAt and ErrorMessage are nullable

### Application-Level Validation

- Transaction type must be 1-9 (enforced by domain entity constructor)
- File status transitions validated by domain methods
- Amount values must be positive (sign determined by type)
- Required fields validated before persistence

### Defense in Depth

- Database constraints provide first line of defense
- Domain entities enforce business rules
- Application services validate complex scenarios
- Both layers work together for data integrity

### Index Optimization Strategy

**Query Performance Indexes**:

1. **File Status Queries** (`IX_Files_Status`):
   - Purpose: Fast filtering of files by processing status
   - Query pattern: `WHERE Status = 'Uploaded'` (pending files)
   - Impact: Supports file processing queue queries

2. **File Upload Timeline** (`IX_Files_UploadedAt`):
   - Purpose: Chronological sorting and date range filtering
   - Query pattern: `ORDER BY UploadedAt DESC` or `WHERE UploadedAt >= @date`
   - Impact: Efficient file history queries

3. **Store Identification** (`stores_name_owner_unique`, UNIQUE):
    - Purpose: Enforce uniqueness by logical store identity (Name + OwnerName)
    - Query pattern: `WHERE name = @name AND owner_name = @owner`
    - Impact: Prevents duplicates without synthetic codes

4. **Store Name Search** (`IX_Stores_Name`):
   - Purpose: Store filtering and searching by name
   - Query pattern: `WHERE Name LIKE @pattern`
   - Impact: Supports store listing and search features

5. **Transaction File Relationship** (`IX_Transactions_FileId`):
   - Purpose: Retrieve all transactions for a specific file
   - Query pattern: `WHERE FileId = @fileId`
   - Impact: File transaction listing and reporting

6. **Transaction Store Relationship** (`IX_Transactions_StoreId`):
   - Purpose: Retrieve all transactions for a specific store
   - Query pattern: `WHERE StoreId = @storeId`
   - Impact: Store transaction history and reporting queries

7. **Transaction Date Filtering** (`IX_Transactions_OccurredAt`):
   - Purpose: Date range queries and chronological sorting
   - Query pattern: `WHERE OccurredAt BETWEEN @start AND @end`
   - Impact: Transaction history and reporting queries

8. **Store-Date Composite** (`IX_Transactions_StoreId_OccurredAt`):
    - Purpose: Optimized store date-range queries
    - Query pattern: `WHERE StoreId = @id AND OccurredAt >= @date`
    - Impact: Most common query pattern for store-centric reports

**Index Coverage**:

- All foreign keys are indexed (FileId, StoreId)
- Frequently filtered columns are indexed (Status, dates)
- Composite index supports most common query pattern
- Unique index enforces business rule (one store per code)

## Entity Framework Core Configuration

### DbContext Implementation

The `ApplicationDbContext` uses Fluent API for entity configuration and relationship mapping.

```csharp
public class ApplicationDbContext : DbContext
{
    public DbSet<File> Files { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // File entity configuration
        modelBuilder.Entity<File>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>();
            entity.Property(e => e.UploadedAt).IsRequired();
            entity.Property(e => e.ProcessedAt).IsRequired(false);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UploadedAt);
        });
        
        // Store entity configuration
        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(14);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(19);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Name);
        });
        
        // Transaction entity configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Amount).IsRequired().HasPrecision(18, 2);
            entity.Property(e => e.OccurredAt).IsRequired();
            entity.Property(e => e.OccurredAtTime).IsRequired();
            entity.Property(e => e.CPF).IsRequired().HasMaxLength(11);
            entity.Property(e => e.Card).IsRequired().HasMaxLength(12);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            // Relationships
            entity.HasOne(e => e.File)
                  .WithMany(f => f.Transactions)
                  .HasForeignKey(e => e.FileId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Store)
                  .WithMany(s => s.Transactions)
                  .HasForeignKey(e => e.StoreId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            // Indexes
            entity.HasIndex(e => e.FileId);
            entity.HasIndex(e => e.StoreId);
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => new { e.StoreId, e.OccurredAt });
        });
    }
    
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }
    
    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);
        
        foreach (var entry in entries)
        {
            if (entry.Entity is Store store)
            {
                if (entry.State == EntityState.Added)
                    store.CreatedAt = DateTime.UtcNow;
                store.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
```

### Entity Models

#### File Entity

```csharp
public class File
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public FileStatus Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Navigation property
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
```

#### Store Entity

```csharp
public class Store
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation property
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
```

#### Transaction Entity

```csharp
public class Transaction
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public Guid StoreId { get; set; }
    public int Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
    public TimeSpan OccurredAtTime { get; set; }
    public string CPF { get; set; }
    public string Card { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public File File { get; set; }
    public Store Store { get; set; }
    
    /// <summary>
    /// Returns the signed amount based on CNAB transaction type rules.
    /// Types 1,4,5,6,7,8 are credits (positive).
    /// Types 2,3,9 are debits (negative).
    /// </summary>
    public decimal GetSignedAmount()
    {
        return Type switch
        {
            2 or 3 or 9 => -Amount,
            _ => Amount
        };
    }
}
```

## Migrations

### EF Core Migration Strategy

- **Version Control**: All migrations tracked in source control
- **Sequential Naming**: Timestamp-based migration names (yyyyMMddHHmmss_MigrationName)
- **Development**: Migrations applied automatically via `dotnet ef database update`
- **Production**: Migrations applied via CI/CD pipeline before deployment
- **Rollback**: Each migration has Down() method for reverting changes

### Initial Migration (20260117200000_InitialSchema)

The initial comprehensive migration creates all schema elements in atomic fashion for production deployment.

**Created Elements**:

1. **PostgreSQL Extensions**:
   - pgcrypto extension for UUID v7 generation

2. **PostgreSQL Functions**:
   - `gen_random_uuid_v7()`: Time-ordered UUID generation (48-bit timestamp + 80-bit random)

3. **Lookup Tables**:
   - `transaction_types` (9 seeded rows, types 1-9)
   - `file_statuses` (4 seeded rows, states Uploaded/Processing/Processed/Rejected)

4. **Core Entity Tables**:
   - `Files` (UUID v7 PK, indexed by StatusCode, UploadedAt, UploadedByUserId)
   - `Stores` (UUID v7 PK, unique composite index on Name+owner_name)
   - `Transactions` (BIGSERIAL PK, indexed by FileId, StoreId, transaction_date, composite StoreId+transaction_date)

5. **Relationships**:
   - Transactions.FileId → Files.Id (CASCADE delete)
   - Transactions.StoreId → Stores.Id (RESTRICT delete)
   - Transactions.transaction_type_code → transaction_types.type_code (RESTRICT delete)
   - Files.StatusCode → file_statuses.status_code (RESTRICT delete)

6. **Indexes Created**: 11 total indexes for query optimization
   - File indexes: status_code, uploaded_at, uploaded_by_user, s3_key (unique)
   - Store indexes: name_owner_unique (composite)
   - Transaction indexes: file_id, store_id, transaction_date, composite store_date
   - Lookup table indexes: transaction_types_code, file_statuses_code

**Migration Characteristics**:

- **Atomic**: All changes in single transaction, either all apply or all rollback
- **Comprehensive**: Contains all initial schema elements needed for application startup
- **Idempotent Down()**: Complete rollback capability with DROP IF EXISTS patterns
- **Seed Data**: Lookup tables pre-populated with all valid values

### Creating New Migrations

```bash
cd src/backend/TransactionProcessor.Api

# Add new migration
dotnet ef migrations add MigrationName \
  --project ../TransactionProcessor.Infrastructure \
  --output-dir ../TransactionProcessor.Infrastructure/Persistence/Migrations
```

### Applying Migrations

**Development** (automatic on application startup with EF Core configuration):

```bash
cd src/backend/TransactionProcessor.Api

# Explicit migration application
dotnet ef database update \
  --project ../TransactionProcessor.Infrastructure
```

**Production** (via deployment pipeline):

```bash
dotnet ef database update --connection "<connection-string>"
```

### Migration Best Practices

- Review generated SQL before applying (`dotnet ef migrations script`)
- One logical change per migration
- Test both Up and Down migrations
- Never edit applied migrations
- Backup database before production migrations

## Transaction Processing Strategy

### Atomic File Processing

**Critical Rule**: All transactions from a single CNAB file must be persisted within a single database transaction.

```csharp
using var dbTransaction = await context.Database.BeginTransactionAsync();
try
{
    // 1. Update file status to Processing
    file.Status = FileStatus.Processing;
    await context.SaveChangesAsync();
    
    // 2. Validate all CNAB lines
    var transactions = ValidateAndParseCnabFile(fileContent);
    
    // 3. Upsert stores (bulk operation)
    foreach (var storeCode in transactions.Select(t => t.StoreCode).Distinct())
    {
        await UpsertStoreAsync(storeCode, storeName);
    }
    
    // 4. Insert transactions (bulk operation)
    await context.Transactions.AddRangeAsync(transactions);
    await context.SaveChangesAsync();
    
    // 5. Update store balances
    await RecalculateStoreBalancesAsync(affectedStoreIds);
    
    // 6. Mark file as processed
    file.Status = FileStatus.Processed;
    file.ProcessedAt = DateTime.UtcNow;
    await context.SaveChangesAsync();
    
    // Commit all changes atomically
    await dbTransaction.CommitAsync();
}
catch (Exception ex)
{
    await dbTransaction.RollbackAsync();
    
    // Mark file as rejected
    file.Status = FileStatus.Rejected;
    file.ErrorMessage = ex.Message;
    await context.SaveChangesAsync();
    
    throw;
}
```

### Transaction Boundaries

- **Begin**: When file status changes to Processing
- **Commit**: After all transactions persisted and reports updated
- **Rollback**: On any validation or persistence error

### ACID Guarantees

- **Atomicity**: All transactions from a file succeed or all fail
- **Consistency**: Aggregations derive consistently from transaction history
- **Isolation**: Concurrent file processing doesn't interfere
- **Durability**: Committed data survives system failures

## Query Patterns and Examples

### Store Aggregation Example (computed)

Calculate store balance by summing signed transaction amounts:

```csharp
// Using GetSignedAmount() method
var balance = await context.Transactions
    .AsNoTracking()
    .Where(t => t.StoreId == storeId)
    .SumAsync(t => 
        t.Type == 2 || t.Type == 3 || t.Type == 9 
            ? -t.Amount 
            : t.Amount
    );
```

**SQL Equivalent**:

```sql
SELECT COALESCE(SUM(
    CASE 
        WHEN "Type" IN (2, 3, 9) THEN -"Amount"
        ELSE "Amount"
    END
), 0) AS Balance
FROM "Transactions"
WHERE "StoreId" = @storeId;
```

### Transaction Filtering with Pagination

```csharp
var transactions = await context.Transactions
    .AsNoTracking()
    .Include(t => t.Store)
    .Include(t => t.File)
    .Where(t => 
        (storeId == null || t.StoreId == storeId) &&
        (startDate == null || t.OccurredAt >= startDate) &&
        (endDate == null || t.OccurredAt <= endDate)
    )
    .OrderByDescending(t => t.OccurredAt)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

### File Processing Status Query

```csharp
var pendingFiles = await context.Files
    .AsNoTracking()
    .Where(f => f.Status == FileStatus.Uploaded)
    .OrderBy(f => f.UploadedAt)
    .ToListAsync();
```

### Store Upsert Pattern

```csharp
var store = await context.Stores
    .FirstOrDefaultAsync(s => s.Code == code);

if (store == null)
{
    store = new Store
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = name
    };
    context.Stores.Add(store);
}
else
{
    store.Name = name;
}

await context.SaveChangesAsync();
```

## Performance Optimization

### Query Optimization

**Critical for performance - follow these practices:**

1. **Use .AsNoTracking() for Read-Only Queries**:
   - Reduces memory consumption by 30-40%
   - Essential for all list/query endpoints
   - Example: `.AsNoTracking().Include(t => t.Store).ToListAsync()`

2. **Avoid N+1 Queries**:
   - Use `.Include()` for eager loading related entities
   - Never query related entities in loops
   - Example: Load transactions with stores in single query

3. **Use Projections for Read-Only Data**:
   - Select only needed columns with `.Select()`
   - Reduces data transfer and memory usage
   - Example: `.Select(s => new { s.Name, s.OwnerName })`

4. **Batch Operations**:
   - Use `AddRangeAsync()` for multiple inserts
   - Single `SaveChangesAsync()` call per batch
   - Critical for file processing performance

5. **Appropriate Indexes**:
   - Index frequently queried columns (status, dates, foreign keys)
   - Composite indexes for multi-column filters
   - Monitor index usage and effectiveness

**Performance Checklist**:

- ✅ All read-only queries use `.AsNoTracking()`
- ✅ Related entities loaded with `.Include()` (no lazy loading)
- ✅ Projections used to fetch only needed data
- ✅ Batch operations for file processing
- ✅ Indexes on frequently queried columns
- ✅ Query logging enabled for slow query detection

### Monitoring

- Query performance monitoring
- Slow query identification
- Index usage analysis
- Connection pool monitoring

## Security

### Database Security

- Encrypted connections (SSL/TLS)
- Role-based access control
- Least privilege principle
- Parameterized queries (prevent SQL injection)

### Data Protection

- Sensitive data encryption at rest
- PII handling compliance
- Audit logging
- Access logging

### DateTime Storage Standard

**Decision**: All timestamps stored as UTC in PostgreSQL

- **Column Type**: `timestamp with time zone`
- **Storage Format**: UTC timezone
- **Application Layer**: Convert to UTC before storing
- **Retrieval**: Return as UTC, client handles timezone conversion

**EF Core Configuration**:

```csharp
entity.Property(e => e.CreatedAt)
    .HasColumnType("timestamp with time zone")
    .HasDefaultValueSql("NOW()");
```

**Benefits**:

- No timezone ambiguity
- Consistent sorting and comparison
- Easy conversion to local times in client
- Standard practice for distributed systems

## Connection Management

### EF Core Connection Pooling

Entity Framework Core automatically manages connection pooling with configurable settings:

```csharp
// Program.cs configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30);
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    }));
```

**Connection Pool Settings**:

- Default pool size: 100 connections (configurable via connection string)
- Connection timeout: 30 seconds
- Idle connection cleanup: Automatic
- Retry on transient failures: 3 attempts with exponential backoff

### Best Practices

- ✅ Use async methods throughout (`*Async()`)
- ✅ Dispose DbContext properly (automatic with DI scopes)
- ✅ Keep transactions short-lived
- ✅ Use `AsNoTracking()` for read-only queries
- ✅ Monitor connection pool exhaustion
- ❌ Don't share DbContext across requests
- ❌ Avoid long-running transactions (>5 seconds)
- ❌ Never block async methods with `.Result` or `.Wait()`

## Backup and Recovery

### Local Development

**Docker Volume Backup**:

```bash
# Backup postgres-data volume
docker run --rm -v postgres-data:/data -v $(pwd):/backup ubuntu tar czf /backup/postgres-backup.tar.gz /data

# Restore from backup
docker run --rm -v postgres-data:/data -v $(pwd):/backup ubuntu tar xzf /backup/postgres-backup.tar.gz -C /
```

### Production (AWS RDS)

**Automated Backups**:

- Retention period: 7-30 days (configurable)
- Backup window: During low-traffic hours
- Point-in-time recovery: Up to 5 minutes before failure
- Multi-AZ deployment: Automatic failover

**Manual Snapshots**:

- Create before major migrations
- Long-term retention (beyond automated window)
- Cross-region replication for disaster recovery

### Recovery Procedures

1. **Identify failure point**: Check CloudWatch logs and metrics
2. **Assess data loss**: Determine last successful transaction
3. **Choose recovery method**: Point-in-time restore vs snapshot
4. **Restore database**: Via AWS Console or CLI
5. **Verify integrity**: Run validation queries
6. **Resume operations**: Update connection strings, restart services

**Recovery Objectives**:

- **RTO** (Recovery Time Objective): < 1 hour
- **RPO** (Recovery Point Objective): < 5 minutes

## Local Development Configuration

### Docker Compose Setup

PostgreSQL 16 runs in Docker for local development:

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: transactionprocessor
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-data:
```

### Connection String

**Development** (appsettings.Development.json):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres;Ssl Mode=Disable"
  }
}
```

**Production** (AWS RDS):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=<rds-endpoint>;Port=5432;Database=transactionprocessor;Username=<user>;Password=<password>;Ssl Mode=Require"
  }
}
```

### Applying Schema Locally

1. Start PostgreSQL:

   ```bash
   docker-compose up -d postgres
   ```

2. Apply migrations:

   ```bash
   cd src/backend
   dotnet ef database update \
     --project TransactionProcessor.Infrastructure \
     --startup-project TransactionProcessor.Api
   ```

3. Verify schema:

   ```bash
   docker exec -it <container-id> psql -U postgres -d transactionprocessor
   \dt  -- List tables
   \d "Files"  -- Describe Files table
   ```

## Production Considerations

### AWS RDS

- Managed PostgreSQL service
- Automated backups
- Multi-AZ for high availability
- Read replicas for scaling
- Monitoring and alerting

### Migration to Production

- Test migrations in staging
- Backup before migration
- Monitor during migration
- Rollback plan ready
