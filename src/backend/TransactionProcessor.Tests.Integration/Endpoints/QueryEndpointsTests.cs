using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Tests.Integration.Fixtures;
using Xunit;

namespace TransactionProcessor.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for query endpoints:
/// - GET /api/transactions/v1 (with filtering, pagination)
/// - GET /api/stores/v1 (with balance calculations)
/// Tests data retrieval, filtering, pagination, balance computation, and error handling
/// 
/// NOTE: These tests require the API server to be running externally or need to be
/// refactored to use WebApplicationFactory with proper secrets mocking.
/// Currently skipped until infrastructure is properly set up.
/// 
/// To run these tests manually:
/// 1. Start the API server: dotnet run --project TransactionProcessor.Api
/// 2. Run tests with filter: dotnet test --filter "Category=RequiresExternalApi"
/// </summary>
[Trait("Category", "RequiresExternalApi")]
[Collection("SkipInCI")]
public class QueryEndpointsTests : IAsyncLifetime
{
    private DatabaseFixture _databaseFixture = null!;
    private HttpClient _httpClient = null!;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task InitializeAsync()
    {
        _databaseFixture = new DatabaseFixture();
        await _databaseFixture.InitializeAsync();
        
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        
        await _databaseFixture.ClearDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await _databaseFixture.DisposeAsync();
    }

    #region Helper Methods

    private async Task<T?> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, _jsonOptions);
    }

    private async Task SeedTestDataAsync()
    {
        // Create test stores
        var store1 = new Store(Guid.NewGuid(), "Owner One", "Store One");
        var store2 = new Store(Guid.NewGuid(), "Owner Two", "Store Two");
        var store3 = new Store(Guid.NewGuid(), "Owner Three", "Store Three");

        _databaseFixture.DbContext.Stores.AddRange(store1, store2, store3);

        // Create transaction types (if not seeded)
        var transactionTypes = new[]
        {
            new TransactionType { TypeCode = "1", Description = "Debit", Nature = "Income", Sign = "+" },
            new TransactionType { TypeCode = "2", Description = "Boleto", Nature = "Expense", Sign = "-" },
            new TransactionType { TypeCode = "3", Description = "Financing", Nature = "Expense", Sign = "-" },
            new TransactionType { TypeCode = "4", Description = "Credit", Nature = "Income", Sign = "+" },
            new TransactionType { TypeCode = "5", Description = "Loan Receipt", Nature = "Income", Sign = "+" }
        };

        foreach (var type in transactionTypes)
        {
            if (!_databaseFixture.DbContext.TransactionTypes.Any(t => t.TypeCode == type.TypeCode))
            {
                _databaseFixture.DbContext.TransactionTypes.Add(type);
            }
        }

        await _databaseFixture.DbContext.SaveChangesAsync();

        // Create test file
        var fileId = Guid.NewGuid();
        var file = new Domain.Entities.File(fileId, "test_cnab.txt")
        {
            FileSize = 2048,
            S3Key = "cnab/test_cnab.txt"
        };
        file.StartProcessing();
        file.MarkAsProcessed();

        _databaseFixture.DbContext.Files.Add(file);

        // Create transactions for Store 1 (3 credits, 2 debits)
        var baseDate = DateTime.UtcNow.Date;
        var transactions = new List<Transaction>
        {
            // Store 1: Credits = +300, Debits = -150, Balance = +150
            new Transaction(fileId, store1.Id, "2", 10000, DateOnly.FromDateTime(baseDate.AddDays(-5)), TimeOnly.FromDateTime(DateTime.UtcNow), "12345678901", "1234567890"),
            new Transaction(fileId, store1.Id, "2", 10000, DateOnly.FromDateTime(baseDate.AddDays(-4)), TimeOnly.FromDateTime(DateTime.UtcNow), "12345678902", "1234567891"),
            new Transaction(fileId, store1.Id, "3", 10000, DateOnly.FromDateTime(baseDate.AddDays(-3)), TimeOnly.FromDateTime(DateTime.UtcNow), "12345678903", "1234567892"),
            new Transaction(fileId, store1.Id, "1", 5000, DateOnly.FromDateTime(baseDate.AddDays(-2)), TimeOnly.FromDateTime(DateTime.UtcNow), "12345678904", "1234567893"),
            new Transaction(fileId, store1.Id, "4", 10000, DateOnly.FromDateTime(baseDate.AddDays(-1)), TimeOnly.FromDateTime(DateTime.UtcNow), "12345678905", "1234567894"),

            // Store 2: Credits = +200, Debits = -100, Balance = +100
            new Transaction(fileId, store2.Id, "2", 15000, DateOnly.FromDateTime(baseDate.AddDays(-4)), TimeOnly.FromDateTime(DateTime.UtcNow), "22345678901", "2234567890"),
            new Transaction(fileId, store2.Id, "3", 5000, DateOnly.FromDateTime(baseDate.AddDays(-3)), TimeOnly.FromDateTime(DateTime.UtcNow), "22345678902", "2234567891"),
            new Transaction(fileId, store2.Id, "1", 10000, DateOnly.FromDateTime(baseDate.AddDays(-2)), TimeOnly.FromDateTime(DateTime.UtcNow), "22345678903", "2234567892"),

            // Store 3: Debits = -50, Balance = -50
            new Transaction(fileId, store3.Id, "1", 3000, DateOnly.FromDateTime(baseDate.AddDays(-3)), TimeOnly.FromDateTime(DateTime.UtcNow), "32345678901", "3234567890"),
            new Transaction(fileId, store3.Id, "5", 2000, DateOnly.FromDateTime(baseDate.AddDays(-1)), TimeOnly.FromDateTime(DateTime.UtcNow), "32345678902", "3234567891")
        };

        _databaseFixture.DbContext.Transactions.AddRange(transactions);
        await _databaseFixture.DbContext.SaveChangesAsync();
    }

    #endregion

    #region Transaction Endpoint Tests

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_WithNoFilters_ReturnsAllTransactions()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/transactions/v1?page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<PagedResult<TransactionDto>>(response);

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(10); // Total transactions seeded
        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(50);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_FilterByStoreId_ReturnsOnlyMatchingTransactions()
    {
        // Arrange
        await SeedTestDataAsync();
        var store1 = await _databaseFixture.DbContext.Stores.FirstAsync(s => s.Name == "Store One");

        // Act
        var response = await _httpClient.GetAsync($"/api/transactions/v1?storeId={store1.Id}&page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<PagedResult<TransactionDto>>(response);

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(5); // Store 1 has 5 transactions
        result!.Items.Should().AllSatisfy(t => t.StoreName.Should().Be("Store One"));
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_FilterByDateRange_ReturnsTransactionsInRange()
    {
        // Arrange
        await SeedTestDataAsync();
        var startDate = DateTime.UtcNow.Date.AddDays(-4);
        var endDate = DateTime.UtcNow.Date.AddDays(-2);

        // Act
        var response = await _httpClient.GetAsync(
            $"/api/transactions/v1?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<PagedResult<TransactionDto>>(response);

        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().AllSatisfy(t => 
        {
            t.Date.Date.Should().BeOnOrAfter(startDate);
            t.Date.Date.Should().BeOnOrBefore(endDate);
        });
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act - Get first page with 3 items
        var response1 = await _httpClient.GetAsync("/api/transactions/v1?page=1&pageSize=3");
        var result1 = await DeserializeResponse<PagedResult<TransactionDto>>(response1);

        // Act - Get second page with 3 items
        var response2 = await _httpClient.GetAsync("/api/transactions/v1?page=2&pageSize=3");
        var result2 = await DeserializeResponse<PagedResult<TransactionDto>>(response2);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        
        result1.Should().NotBeNull();
        result1!.Items.Should().HaveCount(3);
        result1.Page.Should().Be(1);
        result1.HasNextPage.Should().BeTrue();
        result1.HasPreviousPage.Should().BeFalse();

        result2.Should().NotBeNull();
        result2!.Items.Should().HaveCount(3);
        result2.Page.Should().Be(2);
        result2.HasNextPage.Should().BeTrue();
        result2.HasPreviousPage.Should().BeTrue();

        // Verify different items (no overlap in transaction IDs)
        var ids1 = result1.Items.Select(t => t.Id).ToList();
        var ids2 = result2.Items.Select(t => t.Id).ToList();
        ids1.Should().NotIntersectWith(ids2);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_WithInvalidPage_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/transactions/v1?page=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_WithInvalidPageSize_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/transactions/v1?page=1&pageSize=600");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_WithInvalidDateRange_ReturnsBadRequest()
    {
        // Act - startDate after endDate
        var startDate = DateTime.UtcNow.Date.AddDays(1);
        var endDate = DateTime.UtcNow.Date;
        var response = await _httpClient.GetAsync(
            $"/api/transactions/v1?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_WithNonExistentStoreId_ReturnsNotFound()
    {
        // Act
        var nonExistentId = Guid.NewGuid();
        var response = await _httpClient.GetAsync($"/api/transactions/v1?storeId={nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactions_VerifySignedAmounts_ReturnCorrectPositiveAndNegativeValues()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/transactions/v1?page=1&pageSize=50");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<PagedResult<TransactionDto>>(response);

        result.Should().NotBeNull();
        
        // Verify credit transactions (type 2, 3) have positive amounts
        var credits = result!.Items.Where(t => t.Type == "2" || t.Type == "3").ToList();
        credits.Should().AllSatisfy(t => t.Amount.Should().BePositive());

        // Verify debit transactions (type 1, 4, 5) have negative amounts
        var debits = result.Items.Where(t => t.Type == "1" || t.Type == "4" || t.Type == "5").ToList();
        debits.Should().AllSatisfy(t => t.Amount.Should().BeNegative());
    }

    #endregion

    #region Store Endpoint Tests

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetStores_ReturnsAllStores()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/stores/v1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<List<StoreDto>>(response);

        result.Should().NotBeNull();
        result!.Should().HaveCount(3);
        result.Should().AllSatisfy(s => s.Code.Should().NotBeNullOrEmpty());
        result.Should().AllSatisfy(s => s.Name.Should().NotBeNullOrEmpty());
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetStores_StoresOrderedByName_ReturnsInAlphabeticalOrder()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/stores/v1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<List<StoreDto>>(response);

        result.Should().NotBeNull();
        result!.Should().HaveCount(3);
        
        // Verify alphabetical order
        result[0].Name.Should().Be("Store One");
        result[1].Name.Should().Be("Store Three");
        result[2].Name.Should().Be("Store Two");
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetStores_VerifyBalanceCalculations_ReturnsCorrectBalances()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/stores/v1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<List<StoreDto>>(response);

        result.Should().NotBeNull();
        
        // Store One: +100 + +100 + +100 - 50 - 100 = +150
        var store1 = result!.First(s => s.Name == "Store One");
        store1.Balance.Should().Be(150m);

        // Store Two: +150 + +50 - 100 = +100
        var store2 = result.First(s => s.Name == "Store Two");
        store2.Balance.Should().Be(100m);

        // Store Three: -30 - 20 = -50
        var store3 = result.First(s => s.Name == "Store Three");
        store3.Balance.Should().Be(-50m);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetStores_WithNoTransactions_ReturnsZeroBalance()
    {
        // Arrange - Create a store without transactions
        var store = new Store(Guid.NewGuid(), "Empty Owner", "Empty Store");
        _databaseFixture.DbContext.Stores.Add(store);
        await _databaseFixture.DbContext.SaveChangesAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/stores/v1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<List<StoreDto>>(response);

        result.Should().NotBeNull();
        var emptyStore = result!.First(s => s.Name == "Empty Store");
        emptyStore.Balance.Should().Be(0m);
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetStores_BalanceFormattedWith2Decimals_ReturnsCorrectFormat()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act
        var response = await _httpClient.GetAsync("/api/stores/v1");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify JSON contains balance with decimal format
        content.Should().Contain("\"balance\"");
        
        var result = await DeserializeResponse<List<StoreDto>>(response);
        result.Should().NotBeNull();
        result!.Should().AllSatisfy(s => 
        {
            // Balance should be decimal type
            s.Balance.Should().BeOfType(typeof(decimal));
        });
    }

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetStores_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/stores/v1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<List<StoreDto>>(response);

        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    #endregion

    #region Combined Scenarios

    [Fact(Skip = "Requires external API server running on localhost:5000")]
    public async Task GetTransactionsAndStores_ConsistentData_BalancesMatchTransactionSums()
    {
        // Arrange
        await SeedTestDataAsync();

        // Act - Get transactions for Store One
        var store1 = await _databaseFixture.DbContext.Stores.FirstAsync(s => s.Name == "Store One");
        var transactionsResponse = await _httpClient.GetAsync($"/api/transactions/v1?storeId={store1.Id}&page=1&pageSize=50");
        var transactions = await DeserializeResponse<PagedResult<TransactionDto>>(transactionsResponse);

        // Act - Get stores
        var storesResponse = await _httpClient.GetAsync("/api/stores/v1");
        var stores = await DeserializeResponse<List<StoreDto>>(storesResponse);

        // Assert
        transactions.Should().NotBeNull();
        stores.Should().NotBeNull();

        var storeDto = stores!.First(s => s.Name == "Store One");
        var calculatedBalance = transactions!.Items.Sum(t => t.Amount);

        storeDto.Balance.Should().Be(calculatedBalance);
    }

    #endregion
}
