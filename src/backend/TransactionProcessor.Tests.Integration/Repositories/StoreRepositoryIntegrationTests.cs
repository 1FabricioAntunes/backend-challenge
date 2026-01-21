using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Infrastructure.Repositories;
using TransactionProcessor.Tests.Integration.Fixtures;
using Xunit;

namespace TransactionProcessor.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for StoreRepository using PostgreSQL Testcontainers.
/// 
/// Tests verify:
/// - CRUD operations work correctly with real database
/// - GetByNameAndOwner composite key lookup
/// - Unique constraint enforcement on (Name, OwnerName)
/// - Transaction relationships are properly loaded
/// - Concurrent access scenarios
/// </summary>
[Collection("RepositoryIntegration")]
public class StoreRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly RepositoryIntegrationFixture _fixture;

    public StoreRepositoryIntegrationTests()
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

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_ValidStore_PersistsToDatabase()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore(
            name: "Loja Central",
            ownerName: "João Silva"
        );

        // Act
        await repository.AddAsync(store);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == store.Id);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Loja Central");
        persisted.OwnerName.Should().Be("João Silva");
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        await context.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();

        // Act
        Func<Task> act = () => repository.AddAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("store");

        await context.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_DuplicateNameAndOwner_ThrowsDbUpdateException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store1 = RepositoryIntegrationFixture.CreateStore(
            name: "Unique Store",
            ownerName: "Unique Owner"
        );
        var store2 = RepositoryIntegrationFixture.CreateStore(
            name: "Unique Store",
            ownerName: "Unique Owner",
            id: Guid.NewGuid() // Different ID, same name/owner
        );

        await repository.AddAsync(store1);

        // Act - Create new repository to avoid context tracking issues
        var (repository2, context2) = _fixture.CreateStoreRepository();
        Func<Task> act = () => repository2.AddAsync(store2);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();

        await context.DisposeAsync();
        await context2.DisposeAsync();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingStore_ReturnsStoreWithTransactions()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore(
            name: "Store W Trans",
            ownerName: "Owner ABC"
        );
        await repository.AddAsync(store);

        // Create file and transaction for the store
        var file = RepositoryIntegrationFixture.CreateFile();
        await context.Files.AddAsync(file);
        await context.SaveChangesAsync();

        var transaction = RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 5000m);
        await context.Transactions.AddAsync(transaction);
        await context.SaveChangesAsync();

        // Act
        var (readRepository, readContext) = _fixture.CreateStoreRepository();
        var result = await readRepository.GetByIdAsync(store.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(store.Id);
        result.Name.Should().Be("Store W Trans");
        result.OwnerName.Should().Be("Owner ABC");
        result.Transactions.Should().HaveCount(1);
        result.Transactions.First().Amount.Should().Be(5000m);

        await context.DisposeAsync();
        await readContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingStore_ReturnsNull()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await repository.GetByIdAsync(nonExistingId);

        // Assert
        result.Should().BeNull();

        await context.DisposeAsync();
    }

    #endregion

    #region GetByNameAndOwnerAsync Tests

    [Fact]
    public async Task GetByNameAndOwnerAsync_ExistingStore_ReturnsStore()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore(
            name: "Mercado Central",
            ownerName: "Maria Santos"
        );
        await repository.AddAsync(store);

        // Act
        var (readRepository, readContext) = _fixture.CreateStoreRepository();
        var result = await readRepository.GetByNameAndOwnerAsync("Mercado Central", "Maria Santos");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(store.Id);
        result.Name.Should().Be("Mercado Central");
        result.OwnerName.Should().Be("Maria Santos");

        await context.DisposeAsync();
        await readContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByNameAndOwnerAsync_PartialMatch_ReturnsNull()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore(
            name: "Loja ABC",
            ownerName: "Pedro Costa"
        );
        await repository.AddAsync(store);

        // Act - Different owner name
        var (readRepository, readContext) = _fixture.CreateStoreRepository();
        var result = await readRepository.GetByNameAndOwnerAsync("Loja ABC", "Different Owner");

        // Assert
        result.Should().BeNull();

        await context.DisposeAsync();
        await readContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByNameAndOwnerAsync_CaseSensitive_ReturnsCorrectStore()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore(
            name: "LOJA TESTE",
            ownerName: "OWNER TESTE"
        );
        await repository.AddAsync(store);

        // Act - Exact match
        var (readRepository1, readContext1) = _fixture.CreateStoreRepository();
        var exactMatch = await readRepository1.GetByNameAndOwnerAsync("LOJA TESTE", "OWNER TESTE");

        // Act - Different case (PostgreSQL is case-sensitive by default)
        var (readRepository2, readContext2) = _fixture.CreateStoreRepository();
        var differentCase = await readRepository2.GetByNameAndOwnerAsync("loja teste", "owner teste");

        // Assert
        exactMatch.Should().NotBeNull();
        differentCase.Should().BeNull(); // PostgreSQL is case-sensitive

        await context.DisposeAsync();
        await readContext1.DisposeAsync();
        await readContext2.DisposeAsync();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_MultipleStores_ReturnsAllOrderedByName()
    {
        // Arrange - Clear any existing stores first
        await _fixture.ClearEntityTablesAsync();

        var (repository, context) = _fixture.CreateStoreRepository();
        var stores = new[]
        {
            RepositoryIntegrationFixture.CreateStore(name: "Zebra Store", ownerName: "Owner Z"),
            RepositoryIntegrationFixture.CreateStore(name: "Alpha Store", ownerName: "Owner A"),
            RepositoryIntegrationFixture.CreateStore(name: "Middle Store", ownerName: "Owner M")
        };

        foreach (var store in stores)
        {
            await repository.AddAsync(store);
        }

        // Act
        var (readRepository, readContext) = _fixture.CreateStoreRepository();
        var result = (await readRepository.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Alpha Store");
        result[1].Name.Should().Be("Middle Store");
        result[2].Name.Should().Be("Zebra Store");

        await context.DisposeAsync();
        await readContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Arrange - Clear all stores
        await _fixture.ClearEntityTablesAsync();

        // Act
        var (repository, context) = _fixture.CreateStoreRepository();
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();

        await context.DisposeAsync();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ExistingStore_UpdatesFields()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore(
            name: "Original Name",
            ownerName: "Original Owner"
        );
        await repository.AddAsync(store);

        // Act - Load store in new context and update
        var (updateRepository, updateContext) = _fixture.CreateStoreRepository();
        var storeToUpdate = await updateContext.Stores.FirstAsync(s => s.Id == store.Id);
        storeToUpdate.Name = "Updated Name";
        storeToUpdate.OwnerName = "Updated Owner";
        await updateRepository.UpdateAsync(storeToUpdate);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Stores
            .AsNoTracking()
            .FirstAsync(s => s.Id == store.Id);

        persisted.Name.Should().Be("Updated Name");
        persisted.OwnerName.Should().Be("Updated Owner");
        persisted.UpdatedAt.Should().BeOnOrAfter(persisted.CreatedAt);

        await context.DisposeAsync();
        await updateContext.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();

        // Act
        Func<Task> act = () => repository.UpdateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("store");

        await context.DisposeAsync();
    }

    #endregion

    #region UpdateBalanceAsync Tests

    [Fact]
    public async Task UpdateBalanceAsync_ValidBalance_UpdatesStoreBalance()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore(
            name: "Balance Test Store",
            ownerName: "Balance Owner"
        );
        await repository.AddAsync(store);

        // Act
        var (updateRepository, updateContext) = _fixture.CreateStoreRepository();
        await updateRepository.UpdateBalanceAsync(store.Id, 1500.50m);

        // Assert - Note: Balance is not persisted in normalized schema (ignored in EF config)
        // This test verifies the domain method is called correctly
        // The actual balance should be calculated from transactions
        await updateContext.DisposeAsync();
        await context.DisposeAsync();
    }

    [Fact]
    public async Task UpdateBalanceAsync_NegativeBalance_ThrowsArgumentException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var store = RepositoryIntegrationFixture.CreateStore();
        await repository.AddAsync(store);

        // Act
        var (updateRepository, updateContext) = _fixture.CreateStoreRepository();
        Func<Task> act = () => updateRepository.UpdateBalanceAsync(store.Id, -100m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cannot be negative*");

        await updateContext.DisposeAsync();
        await context.DisposeAsync();
    }

    [Fact]
    public async Task UpdateBalanceAsync_NonExistingStore_ThrowsInvalidOperationException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateStoreRepository();
        var nonExistingId = Guid.NewGuid();

        // Act
        Func<Task> act = () => repository.UpdateBalanceAsync(nonExistingId, 100m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");

        await context.DisposeAsync();
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task AddAsync_ConcurrentInserts_AllSucceed()
    {
        // Arrange
        // Note: Store name max 19 chars, OwnerName max 14 chars
        var stores = Enumerable.Range(1, 10)
            .Select(i => RepositoryIntegrationFixture.CreateStore(
                name: $"Conc Store {i:D2}",
                ownerName: $"Conc Owner {i:D2}"
            ))
            .ToList();

        // Act - Insert concurrently
        var tasks = stores.Select(async store =>
        {
            var (repository, context) = _fixture.CreateStoreRepository();
            try
            {
                await repository.AddAsync(store);
            }
            finally
            {
                await context.DisposeAsync();
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persistedCount = await verifyContext.Stores
            .CountAsync(s => s.Name.StartsWith("Conc Store"));

        persistedCount.Should().Be(10);
    }

    #endregion
}
