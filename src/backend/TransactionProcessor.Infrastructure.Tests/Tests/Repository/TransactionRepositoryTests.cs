using System.Linq;
using FluentAssertions;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Infrastructure.Repositories;
using System;
using System.Threading.Tasks;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace TransactionProcessor.Infrastructure.Tests.Tests.Repository;

/// <summary>
/// Repository integration tests for Transaction entity.
/// Tests create/query operations with filters, includes, and pagination against PostgreSQL.
/// </summary>
public class TransactionRepositoryTests : IntegrationTestBase
{
    private TransactionRepository CreateRepository() => new(DbContext);
    private StoreRepository CreateStoreRepository() => new(DbContext);
    private FileRepository CreateFileRepository() => new(DbContext);

    [Fact]
    public async Task AddAsync_Should_Persist_Transaction_Successfully()
    {
        // Arrange
        var repository = CreateRepository();
        var storeRepo = CreateStoreRepository();
        var fileRepo = CreateFileRepository();

        // Create dependencies
        var store = new Store(Guid.NewGuid(), "Owner", "Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await storeRepo.AddAsync(store);
        
        var file = new Domain.Entities.File(Guid.NewGuid(), "transactions.txt");
        await fileRepo.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Create transaction
        var transaction = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 150.50m,
            cpf: "12345678901",
            card: "1234****5678",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));

        // Act
        await repository.AddAsync(transaction);
        await DbContext.SaveChangesAsync();

        // Assert
        transaction.Id.Should().BeGreaterThan(0); // BIGSERIAL auto-generated
        var persisted = await repository.GetByIdAsync(transaction.Id);
        persisted.Should().NotBeNull();
        persisted?.Amount.Should().Be(150.50m);
        persisted?.CPF.Should().Be("12345678901");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Include_File_And_Store_Navigation()
    {
        // Arrange
        var repository = CreateRepository();
        var storeRepo = CreateStoreRepository();
        var fileRepo = CreateFileRepository();

        var store = new Store(Guid.NewGuid(), "Test Owner", "Test Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await storeRepo.AddAsync(store);

        var file = new Domain.Entities.File(Guid.NewGuid(), "test-file.txt");
        await fileRepo.AddAsync(file);
        await DbContext.SaveChangesAsync();

        var transaction = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "2",
            amount: 200.00m,
            cpf: "98765432100",
            card: "9876****4321",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));

        await repository.AddAsync(transaction);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await repository.GetByIdAsync(transaction.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved?.File.Should().NotBeNull();
        retrieved?.File.FileName.Should().Be("test-file.txt");
        retrieved?.Store.Should().NotBeNull();
        retrieved?.Store.Name.Should().Be("Test Store");
        retrieved?.TransactionType.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByFileIdAsync_Should_Return_All_Transactions_For_File()
    {
        // Arrange
        var repository = CreateRepository();
        var storeRepo = CreateStoreRepository();
        var fileRepo = CreateFileRepository();

        var store = new Store(Guid.NewGuid(), "Owner", "Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await storeRepo.AddAsync(store);

        var file = new Domain.Entities.File(Guid.NewGuid(), "multi-transaction.txt");
        await fileRepo.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Create multiple transactions for same file
        var transaction1 = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 100.00m,
            cpf: "11111111111",
            card: "1111****1111",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(10, 0, 0)));

        var transaction2 = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "2",
            amount: 200.00m,
            cpf: "22222222222",
            card: "2222****2222",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(14, 30, 0)));

        await repository.AddAsync(transaction1);
        await repository.AddAsync(transaction2);
        await DbContext.SaveChangesAsync();

        // Act
        var transactions = await repository.GetByFileIdAsync(file.Id);

        // Assert
        transactions.Should().HaveCount(2);
        transactions.Should().BeInAscendingOrder(t => t.TransactionDate);
    }

    [Fact]
    public async Task GetByStoreIdAsync_Should_Filter_By_Store()
    {
        // Arrange
        var repository = CreateRepository();
        var storeRepo = CreateStoreRepository();
        var fileRepo = CreateFileRepository();

        var store1 = new Store(Guid.NewGuid(), "Owner1", "Store1")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var store2 = new Store(Guid.NewGuid(), "Owner2", "Store2")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await storeRepo.AddAsync(store1);
        await storeRepo.AddAsync(store2);

        var file = new Domain.Entities.File(Guid.NewGuid(), "store-filter.txt");
        await fileRepo.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Create transactions for different stores
        var txStore1 = new Transaction(
            fileId: file.Id,
            storeId: store1.Id,
            transactionTypeCode: "1",
            amount: 100.00m,
            cpf: "11111111111",
            card: "1111****1111",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));

        var txStore2 = new Transaction(
            fileId: file.Id,
            storeId: store2.Id,
            transactionTypeCode: "2",
            amount: 200.00m,
            cpf: "22222222222",
            card: "2222****2222",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));

        await repository.AddAsync(txStore1);
        await repository.AddAsync(txStore2);
        await DbContext.SaveChangesAsync();

        // Act
        var store1Transactions = await repository.GetByStoreIdAsync(store1.Id);

        // Assert
        store1Transactions.Should().HaveCount(1);
        store1Transactions.First().StoreId.Should().Be(store1.Id);
    }

    [Fact]
    public async Task GetByStoreIdAsync_Should_Filter_By_Date_Range()
    {
        // Arrange
        var repository = CreateRepository();
        var storeRepo = CreateStoreRepository();
        var fileRepo = CreateFileRepository();

        var store = new Store(Guid.NewGuid(), "Owner", "Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await storeRepo.AddAsync(store);

        var file = new Domain.Entities.File(Guid.NewGuid(), "date-filter.txt");
        await fileRepo.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Create transactions with different dates
        var oldTransaction = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 50.00m,
            cpf: "11111111111",
            card: "1111****1111",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(10, 0, 0)));

        var recentTransaction = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "2",
            amount: 100.00m,
            cpf: "22222222222",
            card: "2222****2222",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(14, 0, 0)));

        await repository.AddAsync(oldTransaction);
        await repository.AddAsync(recentTransaction);
        await DbContext.SaveChangesAsync();

        // Act - Filter for last 5 days
        var startDate = DateTime.UtcNow.AddDays(-5);
        var endDate = DateTime.UtcNow;
        var filteredTransactions = await repository.GetByStoreIdAsync(store.Id, startDate, endDate);

        // Assert
        filteredTransactions.Should().HaveCount(1);
        filteredTransactions.First().Amount.Should().Be(100.00m);
    }

    [Fact]
    public async Task AddRangeAsync_Should_Bulk_Insert_Transactions()
    {
        // Arrange
        var repository = CreateRepository();
        var storeRepo = CreateStoreRepository();
        var fileRepo = CreateFileRepository();

        var store = new Store(Guid.NewGuid(), "Bulk Owner", "Bulk Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await storeRepo.AddAsync(store);

        var file = new Domain.Entities.File(Guid.NewGuid(), "bulk-transactions.txt");
        await fileRepo.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Create batch of transactions
        var transactions = Enumerable.Range(1, 50).Select(i => new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: i * 10.00m,
            cpf: $"{i:D11}",
            card: $"{i:D12}",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(10, i % 60, 0))
        )).ToList();

        // Act
        await repository.AddRangeAsync(transactions);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await repository.GetByFileIdAsync(file.Id);
        persisted.Should().HaveCount(50);
    }

    [Fact]
    public async Task Query_Should_Return_Transactions_Ordered_Chronologically()
    {
        // Arrange
        var repository = CreateRepository();
        var storeRepo = CreateStoreRepository();
        var fileRepo = CreateFileRepository();

        var store = new Store(Guid.NewGuid(), "Order Owner", "Order Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await storeRepo.AddAsync(store);

        var file = new Domain.Entities.File(Guid.NewGuid(), "order-test.txt");
        await fileRepo.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Create transactions in random order
        var tx1 = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 100.00m,
            cpf: "11111111111",
            card: "1111****1111",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(10, 0, 0)));

        var tx2 = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "2",
            amount: 200.00m,
            cpf: "22222222222",
            card: "2222****2222",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(15, 0, 0))); // Later time, same day

        var tx3 = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "3",
            amount: 300.00m,
            cpf: "33333333333",
            card: "3333****3333",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(new TimeSpan(9, 0, 0)));

        await repository.AddAsync(tx2); // Add in non-chronological order
        await repository.AddAsync(tx1);
        await repository.AddAsync(tx3);
        await DbContext.SaveChangesAsync();

        // Act
        var ordered = await repository.GetByFileIdAsync(file.Id);

        // Assert
        ordered.Should().HaveCount(3);
        var list = ordered.ToList();
        list[0].Amount.Should().Be(100.00m); // First by date
        list[1].Amount.Should().Be(200.00m); // Same date, later time
        list[2].Amount.Should().Be(300.00m); // Latest date
    }
}
