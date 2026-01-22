using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using TransactionProcessor.Infrastructure.Persistence;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests.Fixtures;

/// <summary>
/// Database fixture that manages PostgreSQL container lifecycle and migrations.
/// Implements IAsyncLifetime to support async setup/teardown in xUnit.
/// Each test gets a fresh database with all migrations applied.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private ApplicationDbContext? _context;

    /// <summary>
    /// Gets the database connection string.
    /// Available after InitializeAsync() is called.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the DbContext for database operations.
    /// Available after InitializeAsync() is called.
    /// </summary>
    public ApplicationDbContext DbContext
    {
        get => _context ?? throw new InvalidOperationException("DbContext not initialized");
    }

    /// <summary>
    /// Initialize the PostgreSQL container and apply migrations.
    /// Called automatically by xUnit before test execution.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create and start PostgreSQL container
        _container = new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        // Get connection string from container
        ConnectionString = _container.GetConnectionString();

        // Create DbContext options and context
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        _context = new ApplicationDbContext(options);

        // Apply all pending migrations (creates schema and seed data)
        await _context.Database.MigrateAsync();
    }

    /// <summary>
    /// Clean up and stop the PostgreSQL container.
    /// Called automatically by xUnit after test execution.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a fresh DbContext instance for the current test.
    /// Useful when you need a separate context to verify data isolation.
    /// </summary>
    /// <returns>New ApplicationDbContext instance connected to test database</returns>
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// Clears all data from tables for test isolation.
    /// Useful between tests to ensure clean state.
    /// </summary>
    public async Task ClearAllDataAsync()
    {
        if (_context is null)
        {
            throw new InvalidOperationException("DbContext not initialized");
        }

        // Delete in reverse dependency order to respect foreign key constraints
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"NotificationAttempts\"");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Transactions\"");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Files\"");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"Stores\"");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"TransactionTypes\"");
        await _context.Database.ExecuteSqlRawAsync("DELETE FROM \"FileStatuses\"");

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Executes a raw SQL query against the test database.
    /// Useful for complex queries or direct verification.
    /// </summary>
    /// <param name="sql">SQL command to execute</param>
    /// <param name="parameters">Parameters for the SQL command</param>
    public async Task ExecuteSqlAsync(string sql, params object[] parameters)
    {
        if (_context is null)
        {
            throw new InvalidOperationException("DbContext not initialized");
        }

        await _context.Database.ExecuteSqlRawAsync(sql, parameters);
    }
}
