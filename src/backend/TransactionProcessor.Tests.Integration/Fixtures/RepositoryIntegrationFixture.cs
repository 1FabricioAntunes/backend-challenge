using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Persistence;
using TransactionProcessor.Infrastructure.Repositories;
using Xunit;
using FileEntity = TransactionProcessor.Domain.Entities.File;
using TransactionTypeEntity = TransactionProcessor.Domain.Entities.TransactionType;

namespace TransactionProcessor.Tests.Integration.Fixtures;

/// <summary>
/// Integration test fixture for repository tests using PostgreSQL Testcontainers.
/// Implements IAsyncLifetime for proper container lifecycle management.
/// 
/// This fixture:
/// - Starts a PostgreSQL container per test class (isolation)
/// - Applies EF Core migrations to create schema
/// - Seeds lookup tables (transaction_types, file_statuses)
/// - Provides fresh DbContext instances for test isolation
/// - Cleans up containers after tests complete
/// </summary>
public class RepositoryIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private DbContextOptions<ApplicationDbContext>? _dbContextOptions;

    /// <summary>
    /// Connection string for the test database
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    public RepositoryIntegrationFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase($"test_db_{Guid.NewGuid():N}")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();
    }

    /// <summary>
    /// Initialize container and database schema
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        // Create schema using raw SQL to avoid migration InsertData issues
        // The EF migrations have InsertData operations that fail due to column name mapping
        await using var context = CreateDbContext();
        await CreateSchemaWithRawSqlAsync(context);
    }

    /// <summary>
    /// Cleanup container resources
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new DbContext instance for test isolation.
    /// Each test should use a fresh context to avoid state leakage.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        if (_dbContextOptions == null)
            throw new InvalidOperationException("Fixture not initialized. Call InitializeAsync first.");

        return new ApplicationDbContext(_dbContextOptions);
    }

    /// <summary>
    /// Creates a StoreRepository with a fresh DbContext
    /// </summary>
    public (StoreRepository Repository, ApplicationDbContext Context) CreateStoreRepository()
    {
        var context = CreateDbContext();
        return (new StoreRepository(context), context);
    }

    /// <summary>
    /// Creates a TransactionRepository with a fresh DbContext
    /// </summary>
    public (TransactionRepository Repository, ApplicationDbContext Context) CreateTransactionRepository()
    {
        var context = CreateDbContext();
        return (new TransactionRepository(context), context);
    }

    /// <summary>
    /// Creates a FileRepository with a fresh DbContext
    /// </summary>
    public (FileRepository Repository, ApplicationDbContext Context) CreateFileRepository()
    {
        var context = CreateDbContext();
        return (new FileRepository(context), context);
    }

    /// <summary>
    /// Clears all data from entity tables (preserves lookup tables)
    /// Use between tests for isolation when needed
    /// </summary>
    public async Task ClearEntityTablesAsync()
    {
        await using var context = CreateDbContext();
        
        // Clear in order respecting foreign key constraints
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"notification_attempts\" CASCADE");
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Transactions\" CASCADE");
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Files\" CASCADE");
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Stores\" CASCADE");
    }

    /// <summary>
    /// Creates the entire database schema using raw SQL.
    /// This bypasses EF migrations InsertData issues with column name mappings.
    /// </summary>
    private static async Task CreateSchemaWithRawSqlAsync(ApplicationDbContext context)
    {
        // Create lookup tables first
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS transaction_types (
                type_code VARCHAR(10) PRIMARY KEY,
                ""Description"" VARCHAR(100) NOT NULL,
                ""Nature"" VARCHAR(50) NOT NULL,
                ""Sign"" VARCHAR(1) NOT NULL
            );
        ");

        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS file_statuses (
                status_code VARCHAR(50) PRIMARY KEY,
                ""Description"" VARCHAR(100) NOT NULL,
                is_terminal BOOLEAN NOT NULL
            );
        ");

        // Create Stores table
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Stores"" (
                ""Id"" UUID PRIMARY KEY,
                ""Name"" VARCHAR(19) NOT NULL,
                owner_name VARCHAR(14) NOT NULL,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                CONSTRAINT idx_stores_name_owner_unique UNIQUE (""Name"", owner_name)
            );
        ");

        // Create Files table
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Files"" (
                ""Id"" UUID PRIMARY KEY,
                ""FileName"" VARCHAR(255) NOT NULL,
                ""StatusCode"" VARCHAR(50) NOT NULL DEFAULT 'Uploaded',
                ""FileSize"" BIGINT NOT NULL,
                ""S3Key"" VARCHAR(500) NOT NULL UNIQUE,
                ""UploadedByUserId"" UUID,
                ""UploadedAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""ProcessedAt"" TIMESTAMP WITH TIME ZONE,
                ""ErrorMessage"" VARCHAR(1000)
            );
        ");

        // Create Transactions table with BIGSERIAL
        // Note: TransactionTypeTypeCode is the EF shadow property for the TransactionType navigation
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Transactions"" (
                ""Id"" BIGSERIAL PRIMARY KEY,
                ""FileId"" UUID NOT NULL REFERENCES ""Files""(""Id"") ON DELETE CASCADE,
                ""StoreId"" UUID NOT NULL REFERENCES ""Stores""(""Id"") ON DELETE RESTRICT,
                transaction_type_code VARCHAR(10) NOT NULL,
                ""TransactionTypeTypeCode"" VARCHAR(10) REFERENCES transaction_types(type_code),
                ""Amount"" NUMERIC(18,2) NOT NULL,
                transaction_date DATE NOT NULL,
                transaction_time TIME NOT NULL,
                ""CPF"" VARCHAR(11) NOT NULL,
                ""Card"" VARCHAR(12) NOT NULL,
                ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL,
                ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL
            );
        ");

        // Create notification_attempts table
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS notification_attempts (
                ""Id"" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                ""FileId"" UUID NOT NULL REFERENCES ""Files""(""Id"") ON DELETE CASCADE,
                notification_type VARCHAR(50) NOT NULL,
                recipient VARCHAR(500) NOT NULL,
                status VARCHAR(50) NOT NULL,
                attempt_count INTEGER NOT NULL,
                last_attempt_at TIMESTAMP WITH TIME ZONE NOT NULL,
                error_message VARCHAR(1000),
                sent_at TIMESTAMP WITH TIME ZONE
            );
        ");

        // Create indexes
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE INDEX IF NOT EXISTS idx_files_status_code ON ""Files""(""StatusCode"");
            CREATE INDEX IF NOT EXISTS idx_files_uploaded_at ON ""Files""(""UploadedAt"");
            CREATE INDEX IF NOT EXISTS idx_transactions_file_id ON ""Transactions""(""FileId"");
            CREATE INDEX IF NOT EXISTS idx_transactions_store_id ON ""Transactions""(""StoreId"");
            CREATE INDEX IF NOT EXISTS idx_transactions_date ON ""Transactions""(transaction_date);
        ");

        // Seed transaction types
        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO transaction_types (type_code, ""Description"", ""Nature"", ""Sign"") VALUES
            ('1', 'Debit', 'Income', '+'),
            ('2', 'Boleto', 'Expense', '-'),
            ('3', 'Financing', 'Expense', '-'),
            ('4', 'Credit', 'Income', '+'),
            ('5', 'Loan Receipt', 'Income', '+'),
            ('6', 'Sales', 'Income', '+'),
            ('7', 'TED Receipt', 'Income', '+'),
            ('8', 'DOC Receipt', 'Income', '+'),
            ('9', 'Rent', 'Expense', '-')
            ON CONFLICT (type_code) DO NOTHING;
        ");

        // Seed file statuses
        await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO file_statuses (status_code, ""Description"", is_terminal) VALUES
            ('Uploaded', 'File uploaded, awaiting processing', false),
            ('Processing', 'File currently being processed', false),
            ('Processed', 'File successfully processed', true),
            ('Rejected', 'File rejected due to errors', true)
            ON CONFLICT (status_code) DO NOTHING;
        ");
    }

    #region Test Data Builders

    /// <summary>
    /// Creates a valid store entity for testing
    /// </summary>
    public static Store CreateStore(
        string name = "Test Store",
        string ownerName = "Test Owner",
        Guid? id = null)
    {
        return new Store(
            id ?? Guid.NewGuid(),
            ownerName,
            name
        );
    }

    /// <summary>
    /// Creates a valid file entity for testing
    /// </summary>
    public static FileEntity CreateFile(
        string fileName = "test_file.txt",
        Guid? id = null,
        long fileSize = 1024)
    {
        var fileId = id ?? Guid.NewGuid();
        return new FileEntity(fileId, fileName)
        {
            FileSize = fileSize,
            S3Key = $"cnab/{fileId:N}/{fileName}"
        };
    }

    /// <summary>
    /// Creates a valid transaction entity for testing
    /// </summary>
    public static Transaction CreateTransaction(
        Guid fileId,
        Guid storeId,
        string typeCode = "4",
        decimal amount = 10000m,
        DateOnly? transactionDate = null,
        TimeOnly? transactionTime = null,
        string cpf = "12345678901",
        string card = "123456789012")
    {
        return new Transaction(
            fileId,
            storeId,
            typeCode,
            amount,
            transactionDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            transactionTime ?? TimeOnly.FromDateTime(DateTime.UtcNow),
            cpf,
            card
        );
    }

    /// <summary>
    /// Creates a set of transactions with various types for balance calculation testing
    /// </summary>
    public static List<Transaction> CreateTransactionsForBalanceTest(
        Guid fileId,
        Guid storeId)
    {
        var baseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var baseTime = TimeOnly.FromDateTime(DateTime.UtcNow);

        return new List<Transaction>
        {
            // Credit transactions (positive balance impact)
            CreateTransaction(fileId, storeId, "4", 10000m, baseDate, baseTime),  // +100.00
            CreateTransaction(fileId, storeId, "5", 5000m, baseDate, baseTime),   // +50.00
            CreateTransaction(fileId, storeId, "6", 3000m, baseDate, baseTime),   // +30.00
            
            // Debit transactions (negative balance impact)
            CreateTransaction(fileId, storeId, "1", 2000m, baseDate, baseTime),   // -20.00
            CreateTransaction(fileId, storeId, "2", 1500m, baseDate, baseTime),   // -15.00
            CreateTransaction(fileId, storeId, "9", 500m, baseDate, baseTime),    // -5.00
            
            // Net balance: 100 + 50 + 30 - 20 - 15 - 5 = 140.00
        };
    }

    #endregion
}
