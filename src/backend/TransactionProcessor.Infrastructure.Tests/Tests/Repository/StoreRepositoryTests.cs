using FluentAssertions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests.Tests.Repository;

/// <summary>
/// Repository integration tests for Store entity.
/// Tests CRUD operations and query patterns against real PostgreSQL.
/// </summary>
public class StoreRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task CreateStore_Should_Persist_Successfully()
    {
        // Arrange
        var store = new TransactionProcessor.Domain.Entities.Store(id: Guid.NewGuid(), ownerName: "João Silva", name: "Mercado do João")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        DbContext.Stores.Add(store);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.Stores.FindAsync(store.Id);
        persisted.Should().NotBeNull();
        persisted?.Name.Should().Be("Mercado do João");
        persisted?.OwnerName.Should().Be("João Silva");
    }

    [Fact]
    public async Task GetStoreById_Should_Return_Existing_Store()
    {
        // Arrange
        var storeId = Guid.NewGuid();
        var store = new TransactionProcessor.Domain.Entities.Store(id: storeId, ownerName: "Maria Santos", name: "Padaria Central")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.Stores.Add(store);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await DbContext.Stores.FindAsync(storeId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved?.Name.Should().Be("Padaria Central");
    }

    [Fact]
    public async Task DeleteStore_Should_Remove_From_Database()
    {
        // Arrange
        var store = new TransactionProcessor.Domain.Entities.Store(id: Guid.NewGuid(), ownerName: "Owner X", name: "Loja X")
        {
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.Stores.Add(store);
        await DbContext.SaveChangesAsync();

        // Act
        DbContext.Stores.Remove(store);
        await DbContext.SaveChangesAsync();

        // Assert
        var deleted = await DbContext.Stores.FindAsync(store.Id);
        deleted.Should().BeNull();
    }
}
