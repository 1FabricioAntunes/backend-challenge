using System.Net;
using System.Text.Json;
using NUnit.Framework;
using TransactionProcessor.Api.Models;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Tests.Integration.Fixtures;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Tests.Integration.Endpoints;

/// <summary>
/// Integration tests for file endpoints (GET /api/files/v1 and GET /api/files/v1/{id})
/// Tests pagination, response metadata, error handling, and timestamp serialization
/// </summary>
[TestFixture]
public class FileEndpointsIntegrationTests
{
    private DatabaseFixture _databaseFixture = null!;
    private TestDataSeeder _seeder = null!;
    private HttpClient _httpClient = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpAsync()
    {
        _databaseFixture = new DatabaseFixture();
        await _databaseFixture.InitializeAsync();
        
        // Create HttpClient for making requests
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownAsync()
    {
        _httpClient?.Dispose();
        await _databaseFixture.DisposeAsync();
    }

    [SetUp]
    public async Task SetUpAsync()
    {
        await _databaseFixture.ClearDatabaseAsync();
        _seeder = new TestDataSeeder(_databaseFixture.DbContext);
    }

    #region GetFiles Pagination Tests

    [Test]
    public async Task GetFiles_WithDefaultPagination_ReturnsFirstPage()
    {
        // Arrange
        await _seeder.SeedFilesAsync(25);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<FileDto>>(content, 
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Page, Is.EqualTo(1));
            Assert.That(result!.PageSize, Is.EqualTo(10));
            Assert.That(result!.TotalCount, Is.EqualTo(25));
            Assert.That(result!.Items.Count, Is.EqualTo(10));
            Assert.That(result!.TotalPages, Is.EqualTo(3));
            Assert.That(result!.HasPreviousPage, Is.False);
            Assert.That(result!.HasNextPage, Is.True);
        });
    }

    [Test]
    public async Task GetFiles_WithSecondPage_ReturnsCorrectMetadata()
    {
        // Arrange
        await _seeder.SeedFilesAsync(25);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=2&pageSize=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        Assert.Multiple(() =>
        {
            Assert.That(result.Page, Is.EqualTo(2));
            Assert.That(result.Items.Count, Is.EqualTo(10));
            Assert.That(result.HasPreviousPage, Is.True);
            Assert.That(result.HasNextPage, Is.True);
        });
    }

    [Test]
    public async Task GetFiles_WithLastPage_ReturnsCorrectMetadata()
    {
        // Arrange
        await _seeder.SeedFilesAsync(25);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=3&pageSize=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        Assert.Multiple(() =>
        {
            Assert.That(result.Page, Is.EqualTo(3));
            Assert.That(result.Items.Count, Is.EqualTo(5));
            Assert.That(result.HasPreviousPage, Is.True);
            Assert.That(result.HasNextPage, Is.False);
        });
    }

    [Test]
    public async Task GetFiles_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Count, Is.Zero);
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(result.TotalPages, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task GetFiles_WithCustomPageSize_ReturnsCorrectCount()
    {
        // Arrange
        await _seeder.SeedFilesAsync(50);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=25");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);

        Assert.Multiple(() =>
        {
            Assert.That(result.Items.Count, Is.EqualTo(25));
            Assert.That(result.PageSize, Is.EqualTo(25));
            Assert.That(result.TotalPages, Is.EqualTo(2));
        });
    }

    #endregion

    #region Pagination Validation Tests

    [Test]
    public async Task GetFiles_WithInvalidPageNumber_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=0&pageSize=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await DeserializeResponse<ErrorResponse>(response);

        Assert.Multiple(() =>
        {
            Assert.That(error.Error.Code, Is.EqualTo("INVALID_PAGE_NUMBER"));
            Assert.That(error.Error.StatusCode, Is.EqualTo(400));
            Assert.That(error.Error.Message, Contains.Substring("Page"));
        });
    }

    [Test]
    public async Task GetFiles_WithPageSizeTooLarge_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=101");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await DeserializeResponse<ErrorResponse>(response);

        Assert.That(error.Error.Code, Is.EqualTo("INVALID_PAGE_SIZE"));
    }

    [Test]
    public async Task GetFiles_WithPageSizeZero_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=0");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await DeserializeResponse<ErrorResponse>(response);

        Assert.That(error.Error.Code, Is.EqualTo("INVALID_PAGE_SIZE"));
    }

    #endregion

    #region GetFileById Tests

    [Test]
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var fileDto = await DeserializeResponse<FileDto>(response);

        Assert.Multiple(() =>
        {
            Assert.That(fileDto.Id, Is.EqualTo(createdFile.Id));
            Assert.That(fileDto.FileName, Is.EqualTo("test_cnab.txt"));
            Assert.That(fileDto.Status, Is.EqualTo(FileStatus.Processed.ToString()));
            Assert.That(fileDto.TransactionCount, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task GetFileById_WithMissingId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{nonExistentId}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var error = await DeserializeResponse<ErrorResponse>(response);

        Assert.Multiple(() =>
        {
            Assert.That(error.Error.Code, Is.EqualTo("FILE_NOT_FOUND"));
            Assert.That(error.Error.StatusCode, Is.EqualTo(404));
            Assert.That(error.Error.Message, Contains.Substring("not found"));
        });
    }

    [Test]
    public async Task GetFileById_WithEmptyGuid_ReturnsBadRequest()
    {
        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{Guid.Empty}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await DeserializeResponse<ErrorResponse>(response);

        Assert.Multiple(() =>
        {
            Assert.That(error.Error.Code, Is.EqualTo("INVALID_FILE_ID"));
            Assert.That(error.Error.StatusCode, Is.EqualTo(400));
        });
    }

    #endregion

    #region Timestamp Validation Tests

    [Test]
    public async Task GetFiles_IncludesIso8601UtcTimestamps()
    {
        // Arrange
        var testTime = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
        await _seeder.CreateFileAsync(uploadedAt: testTime, status: FileStatus.Uploaded);

        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?page=1&pageSize=10");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await DeserializeResponse<PagedResult<FileDto>>(response);
        var fileDto = result.Items[0];

        Assert.Multiple(() =>
        {
            Assert.That(fileDto.UploadedAt, Is.EqualTo(testTime));
            Assert.That(fileDto.UploadedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var fileDto = await DeserializeResponse<FileDto>(response);

        Assert.Multiple(() =>
        {
            Assert.That(fileDto.ProcessedAt, Is.Not.Null);
            Assert.That(fileDto.ProcessedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(
                Math.Abs((fileDto.ProcessedAt!.Value - processedTime).TotalSeconds),
                Is.LessThan(1));
        });
    }

    [Test]
    public async Task GetFileById_WithoutProcessedAt_ReturnsNull()
    {
        // Arrange
        var file = await _seeder.CreateFileAsync(status: FileStatus.Uploaded);

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{file.Id}");

        // Assert
        var fileDto = await DeserializeResponse<FileDto>(response);
        Assert.That(fileDto.ProcessedAt, Is.Null);
    }

    #endregion

    #region Error Response Serialization Tests

    [Test]
    public async Task ErrorResponse_SerializesWithConsistentStructure()
    {
        // Act
        var response = await _httpClient.GetAsync("/api/files/v1?pageSize=0");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await DeserializeResponse<ErrorResponse>(response);

        Assert.Multiple(() =>
        {
            Assert.That(error.Error, Is.Not.Null);
            Assert.That(error.Error.Code, Is.Not.Null.And.Not.Empty);
            Assert.That(error.Error.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(error.Error.StatusCode, Is.EqualTo(400));
        });
    }

    [Test]
    public async Task FileNotFoundError_IncludesFileId()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _httpClient.GetAsync($"/api/files/v1/{nonExistentId}");

        // Assert
        var error = await DeserializeResponse<ErrorResponse>(response);
        Assert.That(error.Error.Message, Contains.Substring(nonExistentId.ToString()));
    }

    #endregion

    #region File Status Tests

    [Test]
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

        Assert.Multiple(() =>
        {
            Assert.That(statuses.Count, Is.EqualTo(4));
            Assert.That(statuses, Contains.Item(FileStatus.Uploaded.ToString()));
            Assert.That(statuses, Contains.Item(FileStatus.Processing.ToString()));
            Assert.That(statuses, Contains.Item(FileStatus.Processed.ToString()));
            Assert.That(statuses, Contains.Item(FileStatus.Rejected.ToString()));
        });
    }

    [Test]
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

        Assert.Multiple(() =>
        {
            Assert.That(fileDto.Status, Is.EqualTo(FileStatus.Rejected.ToString()));
            Assert.That(fileDto.ErrorMessage, Is.EqualTo(errorMsg));
        });
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
