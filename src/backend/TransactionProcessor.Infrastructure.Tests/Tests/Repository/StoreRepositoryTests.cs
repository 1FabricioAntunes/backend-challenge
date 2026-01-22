using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Infrastructure.Repositories;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests.Tests.Repository;

/// <summary>
/// Repository integration tests for Store entity.
/// Tests CRUD operations and query patterns against real PostgreSQL.
/// </summary>
public class StoreRepositoryTests : IntegrationTestBase
{
    private StoreRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task AddAsync_Should_Persist_Store_Successfully()
    {
        // Arrange
        var repository = CreateRepository();
        var store = new Store(
            id: Guid.NewGuid(),
            ownerName: "João Silva",
            name: "Mercado do João")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await repository.AddAsync(store);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.Stores.FindAsync(store.Id);
        persisted.Should().NotBeNull();
        persisted?.Name.Should().Be("Mercado do João");
        persisted?.OwnerName.Should().Be("João Silva");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Existing_Store()
    {
        // Arrange
        var repository = CreateRepository();
        var storeId = Guid.NewGuid();
        var store = new Store(
            id: storeId,
            ownerName: "Maria Santos",
            name: "Padaria Central")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(store);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await repository.GetByIdAsync(storeId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved?.Name.Should().Be("Padaria Central");
        retrieved?.OwnerName.Should().Be("Maria Santos");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        // Arrange
        var repository = CreateRepository();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAndOwnerAsync_Should_Return_Store_By_Composite_Key()
    {
        // Arrange
        var repository = CreateRepository();
        var store = new Store(
            id: Guid.NewGuid(),
            ownerName: "Carlos Oliveira",
            name: "Supermercado Oliveira")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(store);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await repository.GetByNameAndOwnerAsync("Supermercado Oliveira", "Carlos Oliveira");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved?.Id.Should().Be(store.Id);
        retrieved?.Name.Should().Be("Supermercado Oliveira");
        retrieved?.OwnerName.Should().Be("Carlos Oliveira");
    }

    [Fact]
    public async Task GetByNameAndOwnerAsync_Should_Return_Null_When_Not_Found()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetByNameAndOwnerAsync("NonExistent Store", "NonExistent Owner");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Stores()
    {
        // Arrange
        var repository = CreateRepository();
        var store1 = new Store(Guid.NewGuid(), "Owner1", "Store A")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var store2 = new Store(Guid.NewGuid(), "Owner2", "Store B")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(store1);
        await repository.AddAsync(store2);
        await DbContext.SaveChangesAsync();

        // Act
        var stores = await repository.GetAllAsync();

        // Assert
        stores.Should().HaveCountGreaterOrEqualTo(2);
        stores.Should().Contain(s => s.Name == "Store A");
        stores.Should().Contain(s => s.Name == "Store B");
    }

    [Fact]
    public async Task UpdateAsync_Should_Modify_Store_Properties()
    {
        // Arrange
        var repository = CreateRepository();
        var store = new Store(Guid.NewGuid(), "Original Owner", "Original Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(store);
        await DbContext.SaveChangesAsync();

        // Act - Update properties
        store.UpdatedAt = DateTime.UtcNow;
        await repository.UpdateAsync(store);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await repository.GetByIdAsync(store.Id);
        updated.Should().NotBeNull();
        updated?.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_Store_From_Database()
    {
        // Arrange
        var repository = CreateRepository();
        var store = new Store(Guid.NewGuid(), "Owner X", "Loja X")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(store);
        await DbContext.SaveChangesAsync();

        // Act
        DbContext.Stores.Remove(store);
        await DbContext.SaveChangesAsync();

        // Assert
        var deleted = await repository.GetByIdAsync(store.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task Store_Should_Enforce_Unique_Constraint_On_Name_And_Owner()
    {
        // Arrange
        var repository = CreateRepository();
        var store1 = new Store(Guid.NewGuid(), "Same Owner", "Same Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var store2 = new Store(Guid.NewGuid(), "Same Owner", "Same Store")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(store1);
        await DbContext.SaveChangesAsync();

        // Act & Assert
        await repository.AddAsync(store2);
        var act = async () => await DbContext.SaveChangesAsync();
        
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GetByIdAsync_Should_Include_Transactions_When_Loaded()
    {
        // Arrange
        var repository = CreateRepository();
        var store = new Store(Guid.NewGuid(), "Owner", "Store With Transactions")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(store);
        await DbContext.SaveChangesAsync();

        // Create a file for the transaction
        var file = new Domain.Entities.File(Guid.NewGuid(), "test.txt");
        DbContext.Files.Add(file);
        await DbContext.SaveChangesAsync();

        // Add a transaction to the store
        var transaction = new Transaction(
            fileId: file.Id,
            storeId: store.Id,
            transactionTypeCode: "1",
            amount: 100.00m,
            cpf: "12345678901",
            card: "123456789012",
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            transactionTime: TimeOnly.FromTimeSpan(DateTime.UtcNow.TimeOfDay));

        DbContext.Transactions.Add(transaction);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await repository.GetByIdAsync(store.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved?.Transactions.Should().NotBeNull();
        retrieved?.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByCodeAsync_Should_Return_Null_In_Normalized_Schema()
    {
        // Arrange
        var repository = CreateRepository();

        // Act
        var result = await repository.GetByCodeAsync("ANY-CODE");

        // Assert
        result.Should().BeNull("GetByCode is deprecated and unsupported in normalized schema");
    }
}
