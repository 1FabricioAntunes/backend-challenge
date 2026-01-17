using System.Net;
using System.Text.Json;
using TransactionProcessor.Api.Models;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Tests.Integration.Fixtures;
using Xunit;
using FluentAssertions;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for file endpoints (GET /api/files/v1 and GET /api/files/v1/{id})
/// Tests pagination, response metadata, error handling, and timestamp serialization
/// </summary>
public class FileEndpointsIntegrationTests : IAsyncLifetime
{
    private DatabaseFixture _databaseFixture = null!;
    private TestDataSeeder _seeder = null!;
    private HttpClient _httpClient = null!;

    public async Task InitializeAsync()
    {
        _databaseFixture = new DatabaseFixture();
        await _databaseFixture.InitializeAsync();
        
        // Create HttpClient for making requests
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
        
        // Clear database and seed data for first test
        await _databaseFixture.ClearDatabaseAsync();
        _seeder = new TestDataSeeder(_databaseFixture.DbContext);
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();
        await _databaseFixture.DisposeAsync();
    }

    #region GetFiles Pagination Tests

    [Fact]
    public async Task GetFiles_WithDefaultPagination_ReturnsFirstPage()
    {
        // Arrange
        await _seeder.SeedFilesAsync(25);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        result.Should().NotBeNull();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(25);
        result.Items.Count.Should().Be(10);
        result.TotalPages.Should().Be(3);
        result.HasPreviousPage.Should().BeFalse();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetFiles_WithSecondPage_ReturnsCorrectMetadata()
    {
        // Arrange
        await _seeder.SeedFilesAsync(25);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=2&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        result.Page.Should().Be(2);
        result.Items.Count.Should().Be(10);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetFiles_WithLastPage_ReturnsCorrectMetadata()
    {
        // Arrange
        await _seeder.SeedFilesAsync(25);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=3&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        result.Page.Should().Be(3);
        result.Items.Count.Should().Be(5);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public async Task GetFiles_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        result.Items.Count.Should().Be(0);
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetFiles_WithCustomPageSize_ReturnsCorrectCount()
    {
        // Arrange
        await _seeder.SeedFilesAsync(50);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=25");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        result.Items.Count.Should().Be(25);
        result.PageSize.Should().Be(25);
        result.TotalPages.Should().Be(2);
    }

    #endregion

    #region Pagination Validation Tests

    [Fact]
    public async Task GetFiles_WithInvalidPageNumber_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await DeserializeResponse<ErrorResponse>(response);

        error.Error.Code.Should().Be("INVALID_PAGE_NUMBER");
        error.Error.StatusCode.Should().Be(400);
        error.Error.Message.Should().Contain("Page");
    }

    [Fact]
    public async Task GetFiles_WithPageSizeTooLarge_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=101");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await DeserializeResponse<ErrorResponse>(response);

        error.Error.Code.Should().Be("INVALID_PAGE_SIZE");
    }

    [Fact]
    public async Task GetFiles_WithPageSizeZero_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await DeserializeResponse<ErrorResponse>(response);

        error.Error.Code.Should().Be("INVALID_PAGE_SIZE");
    }

    #endregion

    #region GetFileById Tests

    [Fact]
    public async Task GetFileById_WithValidId_ReturnsFileDetails()
    {
        // Arrange
        var createdFile = await _seeder.CreateFileAsync(
            fileName: "test_cnab.txt",
            status: FileStatus.Processed,
            transactionCount: 5);

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{createdFile.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fileDto = await DeserializeResponse<FileDto>(response);

        fileDto.Id.Should().Be(createdFile.Id);
        fileDto.FileName.Should().Be("test_cnab.txt");
        fileDto.Status.Should().Be(FileStatus.Processed.ToString());
        fileDto.TransactionCount.Should().Be(5);
    }

    [Fact]
    public async Task GetFileById_WithMissingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await DeserializeResponse<ErrorResponse>(response);

        error.Error.Code.Should().Be("FILE_NOT_FOUND");
        error.Error.StatusCode.Should().Be(404);
        error.Error.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetFileById_WithEmptyGuid_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{Guid.Empty}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await DeserializeResponse<ErrorResponse>(response);

        error.Error.Code.Should().Be("INVALID_FILE_ID");
        error.Error.StatusCode.Should().Be(400);
    }

    #endregion

    #region Timestamp Validation Tests

    [Fact]
    public async Task GetFiles_IncludesIso8601UtcTimestamps()
    {
        // Arrange
        var testTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
        await _seeder.CreateFileAsync(uploadedAt: testTime, status: FileStatus.Uploaded);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);
        var fileDto = result.Items[0];

        fileDto.UploadedAt.Should().Be(testTime);
        fileDto.UploadedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task GetFileById_IncludesProcessedAtTimestamp()
    {
        // Arrange
        var uploadedTime = DateTime.UtcNow.AddHours(-1);
        var processedTime = DateTime.UtcNow;
        
        var file = await _seeder.CreateFileAsync(
            status: FileStatus.Processed,
            uploadedAt: uploadedTime,
            processedAt: processedTime);

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{file.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fileDto = await DeserializeResponse<FileDto>(response);

        fileDto.ProcessedAt.Should().NotBeNull();
        fileDto.ProcessedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        Math.Abs((fileDto.ProcessedAt!.Value - processedTime).TotalSeconds).Should().BeLessThan(1);
    }

    [Fact]
    public async Task GetFileById_WithoutProcessedAt_ReturnsNull()
    {
        // Arrange
        var file = await _seeder.CreateFileAsync(status: FileStatus.Uploaded);

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{file.Id}");

        // Assert
        var fileDto = await DeserializeResponse<FileDto>(response);
        fileDto.ProcessedAt.Should().BeNull();
    }

    #endregion

    #region Error Response Serialization Tests

    [Fact]
    public async Task ErrorResponse_SerializesWithConsistentStructure()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?pageSize=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await DeserializeResponse<ErrorResponse>(response);

        error.Error.Should().NotBeNull();
        error.Error.Code.Should().NotBeNullOrEmpty();
        error.Error.Message.Should().NotBeNullOrEmpty();
        error.Error.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task FileNotFoundError_IncludesFileId()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{nonExistentId}");

        // Assert
        var error = await DeserializeResponse<ErrorResponse>(response);
        error.Error.Message.Should().Contain(nonExistentId.ToString());
    }

    #endregion

    #region File Status Tests

    [Fact]
    public async Task GetFiles_IncludesAllFileStatuses()
    {
        // Arrange
        await _seeder.CreateFileAsync(fileName: "uploaded.txt", status: FileStatus.Uploaded);
        await _seeder.CreateFileAsync(fileName: "processing.txt", status: FileStatus.Processing);
        await _seeder.CreateFileAsync(fileName: "processed.txt", status: FileStatus.Processed);
        await _seeder.CreateFileAsync(fileName: "rejected.txt", status: FileStatus.Rejected);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=10");

        // Assert
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);
        var statuses = result.Items.Select(f => f.Status).Distinct().ToList();

        statuses.Should().HaveCount(4);
        statuses.Should().Contain(FileStatus.Uploaded.ToString());
        statuses.Should().Contain(FileStatus.Processing.ToString());
        statuses.Should().Contain(FileStatus.Processed.ToString());
        statuses.Should().Contain(FileStatus.Rejected.ToString());
    }

    [Fact]
    public async Task GetFileById_WithRejectedFile_IncludesErrorMessage()
    {
        // Arrange
        var errorMsg = "Invalid CNAB format: missing required fields";
        var file = await _seeder.CreateFileAsync(
            status: FileStatus.Rejected,
            errorMessage: errorMsg);

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{file.Id}");

        // Assert
        var fileDto = await DeserializeResponse<FileDto>(response);

        fileDto.Status.Should().Be(FileStatus.Rejected.ToString());
        fileDto.ErrorMessage.Should().Be(errorMsg);
    }

    #endregion

    #region Helper Methods

    private async Task<T> DeserializeResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var deserialized = JsonSerializer.Deserialize<T>(content,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return deserialized ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    #endregion
}
