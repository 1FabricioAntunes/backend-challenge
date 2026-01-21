using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Infrastructure.Repositories;
using System;
using System.Threading.Tasks;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests.Tests.Repository;

/// <summary>
/// Integration tests for database transaction rollback scenarios.
/// Ensures data consistency and proper rollback behavior on errors.
/// </summary>
public class TransactionRollbackTests : IntegrationTestBase
{
    [Fact]
    public async Task Transaction_Should_Rollback_On_Exception()
    {
        // Arrange
        var storeRepo = new StoreRepository(DbContext);
        var store = new Store(Guid.NewGuid(), "Test Owner", "Test Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act & Assert
        await using (var transaction = await DbContext.Database.BeginTransactionAsync())
        {
            try
            {
                // Add store
                await storeRepo.AddAsync(store);
                await DbContext.SaveChangesAsync();

                // Verify store is in transaction scope
                var inTransaction = await storeRepo.GetByIdAsync(store.Id);
                inTransaction.Should().NotBeNull();

                // Simulate error - force rollback
                throw new InvalidOperationException("Simulated error");
            }
            catch (InvalidOperationException)
            {
                // Rollback transaction
                await transaction.RollbackAsync();
            }
        }

        // Assert - Store should not exist after rollback
        var afterRollback = await storeRepo.GetByIdAsync(store.Id);
        afterRollback.Should().BeNull();
    }

    [Fact]
    public async Task Bulk_Insert_Should_Rollback_All_On_Constraint_Violation()
    {
        // Arrange
        var storeRepo = new StoreRepository(DbContext);
        var store1 = new Store(Guid.NewGuid(), "Same Owner", "Same Name")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var store2 = new Store(Guid.NewGuid(), "Same Owner", "Same Name") // Duplicate - violates unique constraint
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act & Assert
        await using (var transaction = await DbContext.Database.BeginTransactionAsync())
        {
            try
            {
                await storeRepo.AddAsync(store1);
                await storeRepo.AddAsync(store2);
                await DbContext.SaveChangesAsync();

                // Should not reach here
                Assert.Fail("Expected DbUpdateException");
            }
            catch (DbUpdateException)
            {
                // Expected - rollback
                await transaction.RollbackAsync();
            }
        }

        // Assert - Neither store should exist
        var result1 = await storeRepo.GetByIdAsync(store1.Id);
        var result2 = await storeRepo.GetByIdAsync(store2.Id);
        
        result1.Should().BeNull();
        result2.Should().BeNull();
    }

    [Fact]
    public async Task Transaction_Should_Commit_Successfully_When_No_Errors()
    {
        // Arrange
        var storeRepo = new StoreRepository(DbContext);
        var fileRepo = new FileRepository(DbContext);
        var transactionRepo = new TransactionRepository(DbContext);

        var store = new Store(Guid.NewGuid(), "Commit Owner", "Commit Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var file = new Domain.Entities.File(Guid.NewGuid(), "commit-test.txt");

        // Act
        await using (var transaction = await DbContext.Database.BeginTransactionAsync())
        {
            try
            {
                await storeRepo.AddAsync(store);
                await fileRepo.AddAsync(file);
                await DbContext.SaveChangesAsync();

                // Add transactions
                var tx1 = new Transaction(
                    fileId: file.Id,
                    storeId: store.Id,
                    transactionTypeCode: "1",
                    amount: 100.00m,
                    cpf: "12345678901",
                    card: "1234****5678",
                    transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));

                await transactionRepo.AddAsync(tx1);
                await DbContext.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Assert - All entities should persist after commit
        var persistedStore = await storeRepo.GetByIdAsync(store.Id);
        var persistedFile = await fileRepo.GetByIdAsync(file.Id);
        var persistedTransactions = await transactionRepo.GetByFileIdAsync(file.Id);

        persistedStore.Should().NotBeNull();
        persistedFile.Should().NotBeNull();
        persistedTransactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task Multiple_Operations_Should_Rollback_Atomically()
    {
        // Arrange
        var storeRepo = new StoreRepository(DbContext);
        var fileRepo = new FileRepository(DbContext);
        
        var store = new Store(Guid.NewGuid(), "Multi Op Owner", "Multi Op Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var file1 = new Domain.Entities.File(Guid.NewGuid(), "file1.txt");
        var file2 = new Domain.Entities.File(Guid.NewGuid(), "file2.txt");
        var file3 = new Domain.Entities.File(Guid.NewGuid(), "file3.txt");

        // Act & Assert
        await using (var transaction = await DbContext.Database.BeginTransactionAsync())
        {
            try
            {
                await storeRepo.AddAsync(store);
                await fileRepo.AddAsync(file1);
                await fileRepo.AddAsync(file2);
                await fileRepo.AddAsync(file3);
                await DbContext.SaveChangesAsync();

                // Verify all exist in transaction
                var storeCheck = await storeRepo.GetByIdAsync(store.Id);
                storeCheck.Should().NotBeNull();

                // Force error
                throw new InvalidOperationException("Rollback all operations");
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync();
            }
        }

        // Assert - None of the entities should exist
        var storeAfter = await storeRepo.GetByIdAsync(store.Id);
        var file1After = await fileRepo.GetByIdAsync(file1.Id);
        var file2After = await fileRepo.GetByIdAsync(file2.Id);
        var file3After = await fileRepo.GetByIdAsync(file3.Id);

        storeAfter.Should().BeNull();
        file1After.Should().BeNull();
        file2After.Should().BeNull();
        file3After.Should().BeNull();
    }

    [Fact]
    public async Task Nested_Operations_With_SaveChanges_Should_Rollback_Completely()
    {
        // Arrange
        var storeRepo = new StoreRepository(DbContext);
        var fileRepo = new FileRepository(DbContext);
        var transactionRepo = new TransactionRepository(DbContext);

        var store = new Store(Guid.NewGuid(), "Nested Owner", "Nested Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var file = new Domain.Entities.File(Guid.NewGuid(), "nested.txt");

        // Act & Assert
        await using (var transaction = await DbContext.Database.BeginTransactionAsync())
        {
            try
            {
                // First operation
                await storeRepo.AddAsync(store);
                await DbContext.SaveChangesAsync();

                // Second operation
                await fileRepo.AddAsync(file);
                await DbContext.SaveChangesAsync();

                // Third operation - will fail
                var tx = new Transaction(
                    fileId: Guid.NewGuid(), // Invalid FileId - FK constraint violation
                    storeId: store.Id,
                    transactionTypeCode: "1",
                    amount: 100.00m,
                    cpf: "12345678901",
                    card: "1234****5678",
                    transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));

                await transactionRepo.AddAsync(tx);
                await DbContext.SaveChangesAsync(); // Should throw FK constraint error

                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                // Expected - FK constraint violation
                await transaction.RollbackAsync();
            }
        }

        // Assert - All operations should be rolled back, including earlier SaveChanges
        var storeAfter = await storeRepo.GetByIdAsync(store.Id);
        var fileAfter = await fileRepo.GetByIdAsync(file.Id);

        storeAfter.Should().BeNull("Transaction was rolled back");
        fileAfter.Should().BeNull("Transaction was rolled back");
    }

    [Fact]
    public async Task File_Processing_Workflow_Should_Rollback_On_Transaction_Error()
    {
        // Arrange
        var fileRepo = new FileRepository(DbContext);
        var transactionRepo = new TransactionRepository(DbContext);
        var storeRepo = new StoreRepository(DbContext);

        var file = new Domain.Entities.File(Guid.NewGuid(), "workflow-rollback.txt");
        var store = new Store(Guid.NewGuid(), "Workflow Owner", "Workflow Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Pre-create store outside transaction
        await storeRepo.AddAsync(store);
        await DbContext.SaveChangesAsync();

        // Act & Assert - Simulate file processing workflow
        await using (var transaction = await DbContext.Database.BeginTransactionAsync())
        {
            try
            {
                // Step 1: Add file
                await fileRepo.AddAsync(file);
                await DbContext.SaveChangesAsync();

                // Step 2: Start processing
                file.StartProcessing();
                await fileRepo.UpdateAsync(file);
                await DbContext.SaveChangesAsync();

                // Step 3: Add transactions (simulate parsing)
                var transactions = new[]
                {
                    new Transaction(
                        fileId: file.Id,
                        storeId: store.Id,
                        transactionTypeCode: "1",
                        amount: 100m,
                        cpf: "11111111111",
                        card: "1111****1111",
                        transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
                        transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay)),
                    new Transaction(
                        fileId: file.Id,
                        storeId: store.Id,
                        transactionTypeCode: "2",
                        amount: 200m,
                        cpf: "22222222222",
                        card: "2222****2222",
                        transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
                        transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay))
                };

                await transactionRepo.AddRangeAsync(transactions);
                await DbContext.SaveChangesAsync();

                // Step 4: Simulate error during finalization
                throw new InvalidOperationException("Error during file processing finalization");
            }
            catch (InvalidOperationException)
            {
                await transaction.RollbackAsync();
            }
        }

        // Assert - File and transactions should not exist
        var fileAfter = await fileRepo.GetByIdAsync(file.Id);
        var transactionsAfter = await transactionRepo.GetByFileIdAsync(file.Id);

        fileAfter.Should().BeNull("File creation should be rolled back");
        transactionsAfter.Should().BeEmpty("Transactions should be rolled back");
        
        // Store should still exist (created outside transaction)
        var storeAfter = await storeRepo.GetByIdAsync(store.Id);
        storeAfter.Should().NotBeNull("Store was created before transaction");
    }

    [Fact]
    public async Task Concurrent_Transaction_Isolation_Should_Prevent_Dirty_Reads()
    {
        // Arrange
        var storeRepo = new StoreRepository(DbContext);
        var store = new Store(Guid.NewGuid(), "Isolation Owner", "Isolation Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Start transaction but don't commit
        await using (var transaction = await DbContext.Database.BeginTransactionAsync())
        {
            await storeRepo.AddAsync(store);
            await DbContext.SaveChangesAsync();

            // Create new context to simulate concurrent request
            var newContext = DatabaseFixture.CreateContext();
            var newStoreRepo = new StoreRepository(newContext);

            // Try to read from concurrent context (should not see uncommitted data)
            var concurrentRead = await newStoreRepo.GetByIdAsync(store.Id);
            concurrentRead.Should().BeNull("Should not see uncommitted transaction data");

            // Rollback
            await transaction.RollbackAsync();
        }

        // Assert
        var finalRead = await storeRepo.GetByIdAsync(store.Id);
        finalRead.Should().BeNull("Transaction was rolled back");
    }
}
