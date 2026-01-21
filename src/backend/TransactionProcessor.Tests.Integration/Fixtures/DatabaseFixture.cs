using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Tests.Integration.Fixtures;

/// <summary>
/// Test fixture managing PostgreSQL test container and database context
/// Handles setup, teardown, and database initialization for integration tests
/// </summary>
public class DatabaseFixture : IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("transactionprocessor_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private ApplicationDbContext? _dbContext;
    private bool _initialized = false;

    /// <summary>
    /// Gets the database context for executing queries and commands
    /// </summary>
    public ApplicationDbContext DbContext
    {
        get => _dbContext ?? throw new InvalidOperationException("DbContext not initialized. Call InitializeAsync first.");
        set => _dbContext = value;
    }

    /// <summary>
    /// Connection string for the test database
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Initializes the test container and database
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        
        await _container.StartAsync();

        // Create DbContext with test connection string
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        DbContext = new ApplicationDbContext(options);

        // Create schema using raw SQL to avoid migration InsertData issues
        await CreateSchemaWithRawSqlAsync(DbContext);
        _initialized = true;
    }

    /// <summary>
    /// Creates the database schema using raw SQL.
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
            ('1', 'Débito', 'Expense', '-'),
            ('2', 'Boleto', 'Expense', '-'),
            ('3', 'Financiamento', 'Expense', '-'),
            ('4', 'Crédito', 'Income', '+'),
            ('5', 'Recebimento Empr.', 'Income', '+'),
            ('6', 'Vendas', 'Income', '+'),
            ('7', 'Recebimento TED', 'Income', '+'),
            ('8', 'Recebimento DOC', 'Income', '+'),
            ('9', 'Aluguel', 'Expense', '-')
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

    /// <summary>
    /// Cleans up the test container and resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (DbContext != null)
        {
            await DbContext.DisposeAsync();
        }

        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Clears all data from the database for test isolation
    /// </summary>
    public async Task ClearDatabaseAsync()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(ConnectionString)
                .Options);

        // Delete all transactions first (foreign key constraint)
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Transactions\" CASCADE");

        // Delete all files
        await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Files\" CASCADE");

        await context.SaveChangesAsync();
    }
}
