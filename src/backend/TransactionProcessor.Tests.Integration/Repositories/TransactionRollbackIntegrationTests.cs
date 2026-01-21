using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Persistence;
using TransactionProcessor.Infrastructure.Repositories;
using TransactionProcessor.Tests.Integration.Fixtures;
using Xunit;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for database transaction rollback scenarios.
/// 
/// Tests verify:
/// - All-or-nothing file processing semantics
/// - Proper rollback on validation failures
/// - Proper rollback on database constraint violations
/// - File status transitions during rollback scenarios
/// - Data consistency after failed operations
/// </summary>
[Collection("RepositoryIntegration")]
public class TransactionRollbackIntegrationTests : IAsyncLifetime
{
    private readonly RepositoryIntegrationFixture _fixture;

    public TransactionRollbackIntegrationTests()
    {
        _fixture = new RepositoryIntegrationFixture();
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    #region Helper Methods

    /// <summary>
    /// Calculate balance from transaction type codes directly
    /// Credit types (4,5,6,7,8): positive balance impact
    /// Debit types (1,2,3,9): negative balance impact
    /// </summary>
    private static decimal CalculateBalanceFromTypeCodes(IEnumerable<Transaction> transactions)
    {
        return transactions.Sum(t =>
        {
            var isDebit = t.TransactionTypeCode == "1" || t.TransactionTypeCode == "2" ||
                          t.TransactionTypeCode == "3" || t.TransactionTypeCode == "9";
            return isDebit ? -(t.Amount / 100m) : (t.Amount / 100m);
        });
    }

    #endregion

    #region Basic Rollback Tests

    [Fact]
    public async Task Transaction_WhenExceptionOccurs_RollsBackAllChanges()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var store = RepositoryIntegrationFixture.CreateStore();
        await context.Stores.AddAsync(store);
        
        var file = RepositoryIntegrationFixture.CreateFile();
        await context.Files.AddAsync(file);
        await context.SaveChangesAsync();

        var initialTransactionCount = await context.Transactions.CountAsync();

        // Act - Start a transaction and simulate failure
        await using var transactionContext = _fixture.CreateDbContext();
        await using var dbTransaction = await transactionContext.Database.BeginTransactionAsync();

        try
        {
            // Add some valid transactions
            var transaction1 = RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 1000m);
            var transaction2 = RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "5", 2000m);
            
            await transactionContext.Transactions.AddRangeAsync(transaction1, transaction2);
            await transactionContext.SaveChangesAsync();

            // Verify transactions are visible within the transaction
            var countWithinTransaction = await transactionContext.Transactions.CountAsync();
            countWithinTransaction.Should().Be(initialTransactionCount + 2);

            // Simulate failure - throw exception
            throw new InvalidOperationException("Simulated processing error");
        }
        catch (InvalidOperationException)
        {
            await dbTransaction.RollbackAsync();
        }

        // Assert - Verify rollback
        await using var verifyContext = _fixture.CreateDbContext();
        var finalCount = await verifyContext.Transactions.CountAsync();
        finalCount.Should().Be(initialTransactionCount); // No new transactions persisted
    }

    [Fact]
    public async Task Transaction_WhenCommitted_PersistsAllChanges()
    {
        // Arrange
        await using var context = _fixture.CreateDbContext();
        var store = RepositoryIntegrationFixture.CreateStore();
        await context.Stores.AddAsync(store);
        
        var file = RepositoryIntegrationFixture.CreateFile();
        await context.Files.AddAsync(file);
        await context.SaveChangesAsync();

        var initialCount = await context.Transactions.CountAsync();

        // Act - Start a transaction and commit
        await using var transactionContext = _fixture.CreateDbContext();
        await using var dbTransaction = await transactionContext.Database.BeginTransactionAsync();

        var transaction1 = RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 1000m);
        var transaction2 = RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "5", 2000m);
        
        await transactionContext.Transactions.AddRangeAsync(transaction1, transaction2);
        await transactionContext.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        // Assert - Verify persistence
        await using var verifyContext = _fixture.CreateDbContext();
        var finalCount = await verifyContext.Transactions.CountAsync();
        finalCount.Should().Be(initialCount + 2);
    }

    #endregion

    #region File Processing Rollback Scenarios

    [Fact]
    public async Task FileProcessing_WhenValidationFails_RollsBackAllTransactionsAndUpdatesStatus()
    {
        // Arrange - Create file in Uploaded state
        await using var setupContext = _fixture.CreateDbContext();
        var store = RepositoryIntegrationFixture.CreateStore();
        await setupContext.Stores.AddAsync(store);
        
        var file = RepositoryIntegrationFixture.CreateFile("validation_fail_test.txt");
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Act - Simulate file processing with validation failure
        await using var processContext = _fixture.CreateDbContext();
        await using var dbTransaction = await processContext.Database.BeginTransactionAsync();

        try
        {
            // Load file and start processing
            var fileToProcess = await processContext.Files.FirstAsync(f => f.Id == file.Id);
            fileToProcess.StartProcessing();
            await processContext.SaveChangesAsync();

            // Add some transactions
            var validTransactions = new List<Transaction>
            {
                RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 1000m),
                RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "5", 2000m),
            };
            await processContext.Transactions.AddRangeAsync(validTransactions);
            await processContext.SaveChangesAsync();

            // Simulate validation failure on line 15
            throw new InvalidOperationException("CNAB validation failed: Line 15 has invalid type code X");
        }
        catch (InvalidOperationException ex)
        {
            // Rollback transaction
            await dbTransaction.RollbackAsync();

            // Update file status to Rejected (in separate transaction)
            await using var rejectContext = _fixture.CreateDbContext();
            var fileToReject = await rejectContext.Files.FirstAsync(f => f.Id == file.Id);
            fileToReject.StartProcessing(); // Need to re-apply status transition
            fileToReject.MarkAsRejected(ex.Message);
            await rejectContext.SaveChangesAsync();
        }

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        
        // File should be in Rejected state with error message
        var persistedFile = await verifyContext.Files
            .AsNoTracking()
            .FirstAsync(f => f.Id == file.Id);
        
        persistedFile.StatusCode.Should().Be(FileStatusCode.Rejected);
        persistedFile.ErrorMessage.Should().Contain("validation failed");
        persistedFile.ProcessedAt.Should().NotBeNull();

        // No transactions should be persisted
        var transactionCount = await verifyContext.Transactions
            .CountAsync(t => t.FileId == file.Id);
        transactionCount.Should().Be(0);
    }

    [Fact]
    public async Task FileProcessing_WhenConstraintViolation_RollsBackAndPreservesDataIntegrity()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var store = RepositoryIntegrationFixture.CreateStore();
        await setupContext.Stores.AddAsync(store);
        
        var file = RepositoryIntegrationFixture.CreateFile("constraint_violation_test.txt");
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        var initialTransactionCount = await setupContext.Transactions.CountAsync();

        // Act - Try to insert transaction with invalid StoreId (FK constraint violation)
        await using var processContext = _fixture.CreateDbContext();
        await using var dbTransaction = await processContext.Database.BeginTransactionAsync();

        var wasRolledBack = false;
        try
        {
            // Add valid transactions first
            var validTransaction = RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 1000m);
            await processContext.Transactions.AddAsync(validTransaction);
            await processContext.SaveChangesAsync();

            // Add invalid transaction (non-existing store)
            var invalidTransaction = RepositoryIntegrationFixture.CreateTransaction(
                file.Id, 
                Guid.NewGuid(), // Non-existing store ID
                "5", 
                2000m
            );
            await processContext.Transactions.AddAsync(invalidTransaction);
            await processContext.SaveChangesAsync(); // This should throw

            await dbTransaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync();
            wasRolledBack = true;
        }

        // Assert
        wasRolledBack.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext();
        var finalCount = await verifyContext.Transactions.CountAsync();
        finalCount.Should().Be(initialTransactionCount); // No transactions persisted
    }

    [Fact]
    public async Task FileProcessing_PartialFailure_RollsBackEntireBatch()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var store = RepositoryIntegrationFixture.CreateStore();
        await setupContext.Stores.AddAsync(store);
        
        var file = RepositoryIntegrationFixture.CreateFile("partial_failure_test.txt");
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Act - Process batch with one failing transaction in the middle
        await using var processContext = _fixture.CreateDbContext();
        await using var dbTransaction = await processContext.Database.BeginTransactionAsync();

        var wasRolledBack = false;
        try
        {
            // Successfully add first batch
            var batch1 = Enumerable.Range(1, 50)
                .Select(i => RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 1000m + i))
                .ToList();
            await processContext.Transactions.AddRangeAsync(batch1);
            await processContext.SaveChangesAsync();

            // Second batch includes invalid transaction
            var invalidBatch = new List<Transaction>
            {
                RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "5", 2000m),
                RepositoryIntegrationFixture.CreateTransaction(file.Id, Guid.NewGuid(), "6", 3000m), // Invalid store
                RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "7", 4000m),
            };
            await processContext.Transactions.AddRangeAsync(invalidBatch);
            await processContext.SaveChangesAsync(); // This should fail

            await dbTransaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync();
            wasRolledBack = true;
        }

        // Assert
        wasRolledBack.Should().BeTrue();

        await using var verifyContext = _fixture.CreateDbContext();
        var transactionCount = await verifyContext.Transactions.CountAsync(t => t.FileId == file.Id);
        transactionCount.Should().Be(0); // All 50+ transactions should be rolled back
    }

    #endregion

    #region Store Upsert Rollback Tests

    [Fact]
    public async Task StoreCreation_WhenDuplicateKeyViolation_RollsBackAndAllowsRetry()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var existingStore = RepositoryIntegrationFixture.CreateStore(
            name: "Existing Store",
            ownerName: "Existing Owner"
        );
        await setupContext.Stores.AddAsync(existingStore);
        await setupContext.SaveChangesAsync();

        var file = RepositoryIntegrationFixture.CreateFile();
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Act - Try to create store with same name/owner (constraint violation)
        await using var processContext = _fixture.CreateDbContext();
        await using var dbTransaction = await processContext.Database.BeginTransactionAsync();

        var wasRolledBack = false;
        try
        {
            var duplicateStore = RepositoryIntegrationFixture.CreateStore(
                name: "Existing Store",
                ownerName: "Existing Owner",
                id: Guid.NewGuid()
            );
            await processContext.Stores.AddAsync(duplicateStore);
            await processContext.SaveChangesAsync(); // This should throw

            await dbTransaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await dbTransaction.RollbackAsync();
            wasRolledBack = true;
        }

        // Assert
        wasRolledBack.Should().BeTrue();

        // Retry with proper upsert pattern
        await using var retryContext = _fixture.CreateDbContext();
        var existingOrNew = await retryContext.Stores
            .FirstOrDefaultAsync(s => s.Name == "Existing Store" && s.OwnerName == "Existing Owner");
        
        existingOrNew.Should().NotBeNull();
        existingOrNew!.Id.Should().Be(existingStore.Id); // Should get existing store
    }

    #endregion

    #region Concurrent Transaction Tests

    [Fact]
    public async Task ConcurrentProcessing_IsolatedTransactions_NoInterference()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        
        var store1 = RepositoryIntegrationFixture.CreateStore(name: "Store1", ownerName: "Owner1");
        var store2 = RepositoryIntegrationFixture.CreateStore(name: "Store2", ownerName: "Owner2");
        await setupContext.Stores.AddRangeAsync(store1, store2);
        
        var file1 = RepositoryIntegrationFixture.CreateFile("file1.txt");
        var file2 = RepositoryIntegrationFixture.CreateFile("file2.txt");
        await setupContext.Files.AddRangeAsync(file1, file2);
        await setupContext.SaveChangesAsync();

        // Act - Process two files concurrently, one succeeds, one fails
        var task1 = ProcessFileAsync(file1.Id, store1.Id, shouldSucceed: true);
        var task2 = ProcessFileAsync(file2.Id, store2.Id, shouldSucceed: false);

        await Task.WhenAll(task1, task2);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        
        // File 1 should have transactions
        var file1Transactions = await verifyContext.Transactions
            .CountAsync(t => t.FileId == file1.Id);
        file1Transactions.Should().Be(5);

        // File 2 should have no transactions (rolled back)
        var file2Transactions = await verifyContext.Transactions
            .CountAsync(t => t.FileId == file2.Id);
        file2Transactions.Should().Be(0);
    }

    private async Task ProcessFileAsync(Guid fileId, Guid storeId, bool shouldSucceed)
    {
        await using var context = _fixture.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var transactions = Enumerable.Range(1, 5)
                .Select(i => RepositoryIntegrationFixture.CreateTransaction(fileId, storeId, "4", 1000m * i))
                .ToList();

            await context.Transactions.AddRangeAsync(transactions);
            await context.SaveChangesAsync();

            if (!shouldSucceed)
            {
                throw new InvalidOperationException("Simulated failure");
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
        }
    }

    #endregion

    #region Balance Consistency Tests

    [Fact]
    public async Task Rollback_PreservesStoreBalanceConsistency()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var store = RepositoryIntegrationFixture.CreateStore();
        await setupContext.Stores.AddAsync(store);
        
        var file1 = RepositoryIntegrationFixture.CreateFile("success_file.txt");
        await setupContext.Files.AddAsync(file1);
        await setupContext.SaveChangesAsync();

        // Add initial transactions
        var initialTransactions = RepositoryIntegrationFixture.CreateTransactionsForBalanceTest(file1.Id, store.Id);
        await setupContext.Transactions.AddRangeAsync(initialTransactions);
        await setupContext.SaveChangesAsync();

        // Calculate initial balance using transaction type codes
        await using var balanceContext = _fixture.CreateDbContext();
        var initialTransactionsList = await balanceContext.Transactions
            .Where(t => t.StoreId == store.Id)
            .ToListAsync();
        
        decimal initialBalance = CalculateBalanceFromTypeCodes(initialTransactionsList);

        // Act - Try to add more transactions but fail
        await using var processContext = _fixture.CreateDbContext();
        await using var dbTransaction = await processContext.Database.BeginTransactionAsync();

        var file2 = RepositoryIntegrationFixture.CreateFile("failed_file.txt");
        await processContext.Files.AddAsync(file2);
        await processContext.SaveChangesAsync();

        try
        {
            // Add transactions that would affect balance
            var newTransactions = new List<Transaction>
            {
                RepositoryIntegrationFixture.CreateTransaction(file2.Id, store.Id, "4", 50000m), // +500
                RepositoryIntegrationFixture.CreateTransaction(file2.Id, store.Id, "1", 20000m), // -200
            };
            await processContext.Transactions.AddRangeAsync(newTransactions);
            await processContext.SaveChangesAsync();

            // Simulate failure
            throw new InvalidOperationException("Processing failed");
        }
        catch
        {
            await dbTransaction.RollbackAsync();
        }

        // Assert - Balance should be unchanged
        await using var verifyContext = _fixture.CreateDbContext();
        var finalTransactionsList = await verifyContext.Transactions
            .Where(t => t.StoreId == store.Id)
            .ToListAsync();
        
        decimal finalBalance = CalculateBalanceFromTypeCodes(finalTransactionsList);
        
        finalBalance.Should().Be(initialBalance);
    }

    #endregion

    #region SavePoint Tests (Advanced Transaction Control)

    [Fact]
    public async Task SavePoint_AllowsPartialRollback()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var store = RepositoryIntegrationFixture.CreateStore();
        await setupContext.Stores.AddAsync(store);
        
        var file = RepositoryIntegrationFixture.CreateFile();
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Note: PostgreSQL supports savepoints which allow partial rollback
        // EF Core doesn't directly support savepoints, but we can demonstrate
        // the concept using nested transactions or multiple commit points

        // Act - Process in stages
        await using var context = _fixture.CreateDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync();

        // Stage 1: Add first batch
        var batch1 = Enumerable.Range(1, 3)
            .Select(i => RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 1000m))
            .ToList();
        await context.Transactions.AddRangeAsync(batch1);
        await context.SaveChangesAsync();

        // Create a savepoint by committing and starting new transaction
        await transaction.CommitAsync();

        // Verify first batch is persisted
        await using var verifyContext1 = _fixture.CreateDbContext();
        var countAfterBatch1 = await verifyContext1.Transactions.CountAsync(t => t.FileId == file.Id);
        countAfterBatch1.Should().Be(3);

        // Stage 2: Try to add second batch (simulate failure)
        await using var context2 = _fixture.CreateDbContext();
        await using var transaction2 = await context2.Database.BeginTransactionAsync();

        try
        {
            var batch2 = Enumerable.Range(1, 3)
                .Select(i => RepositoryIntegrationFixture.CreateTransaction(file.Id, Guid.NewGuid(), "5", 2000m)) // Invalid store
                .ToList();
            await context2.Transactions.AddRangeAsync(batch2);
            await context2.SaveChangesAsync(); // Should fail

            await transaction2.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction2.RollbackAsync();
        }

        // Assert - First batch should still be persisted
        await using var verifyContext2 = _fixture.CreateDbContext();
        var finalCount = await verifyContext2.Transactions.CountAsync(t => t.FileId == file.Id);
        finalCount.Should().Be(3); // Only batch 1
    }

    #endregion
}
