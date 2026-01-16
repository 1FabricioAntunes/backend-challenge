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

        // Apply migrations and create schema
        await DbContext.Database.MigrateAsync();
        _initialized = true;
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
