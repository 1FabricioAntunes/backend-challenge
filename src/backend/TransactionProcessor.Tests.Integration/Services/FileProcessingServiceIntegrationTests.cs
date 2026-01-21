using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Application.Models;
using TransactionProcessor.Application.Services;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Persistence;
using Xunit;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Tests.Integration.Services;

/// <summary>
/// Integration tests for file processing service.
/// Tests CNAB file processing pipeline with mocked dependencies.
/// 
/// Note: These tests use InMemory database configured to ignore transaction warnings
/// since InMemory provider doesn't support database transactions.
/// </summary>
public class FileProcessingServiceIntegrationTests
{
    private readonly Mock<IFileRepository> _fileRepositoryMock = new();
    private readonly Mock<IStoreRepository> _storeRepositoryMock = new();
    private readonly Mock<ITransactionRepository> _transactionRepositoryMock = new();
    private readonly Mock<IFileStorageService> _storageServiceMock = new();
    private readonly Mock<ICNABParser> _parserMock = new();
    private readonly Mock<ICNABValidator> _validatorMock = new();
    private readonly Mock<ILogger<FileProcessingService>> _loggerMock = new();

    private readonly FileProcessingService _service;

    public FileProcessingServiceIntegrationTests()
    {
        // Create in-memory database for tests with transaction warning suppressed
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        
        var dbContext = new ApplicationDbContext(options);
        
        _service = new FileProcessingService(
            _fileRepositoryMock.Object,
            _storeRepositoryMock.Object,
            _transactionRepositoryMock.Object,
            _storageServiceMock.Object,
            _parserMock.Object,
            _validatorMock.Object,
            dbContext,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessFileAsync_FileNotFound_ReturnsFailure()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var s3Key = "cnab/file.txt";
        var fileName = "file.txt";
        var correlationId = Guid.NewGuid().ToString();

        _fileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId))
            .ReturnsAsync((FileEntity?)null);

        // Act
        var result = await _service.ProcessFileAsync(fileId, s3Key, fileName, correlationId);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ProcessFileAsync_FileAlreadyProcessed_SkipsProcessing()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var s3Key = "cnab/file.txt";
        var fileName = "file.txt";
        var correlationId = Guid.NewGuid().ToString();

        // Create file and transition through proper states: Uploaded -> Processing -> Processed
        var file = new FileEntity(fileId, fileName);
        file.StartProcessing();  // Uploaded -> Processing
        file.MarkAsProcessed();  // Processing -> Processed

        _fileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId))
            .ReturnsAsync(file);

        // Act
        var result = await _service.ProcessFileAsync(fileId, s3Key, fileName, correlationId);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        // Storage should not be accessed for already processed file
        _storageServiceMock.Verify(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessFileAsync_ValidationError_MarkFileAsRejected()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var s3Key = "cnab/file.txt";
        var fileName = "file.txt";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);

        _fileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId))
            .ReturnsAsync(file);

        // Setup parser to return validation error
        var validationResult = new CNABValidationResult
        {
            IsValid = false,
            Errors = new() { "Line 1: Invalid format" }
        };

        using var fileStream = new MemoryStream();
        _storageServiceMock
            .Setup(x => x.DownloadAsync(s3Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        _parserMock
            .Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _service.ProcessFileAsync(fileId, s3Key, fileName, correlationId);

        // Assert
        result.Success.Should().BeFalse();
        result.ValidationErrors.Should().NotBeEmpty();
        result.ValidationErrors[0].Should().Contain("Invalid format");

        // Verify file was marked as Rejected
        _fileRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<FileEntity>(f => f.StatusCode == FileStatusCode.Rejected)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessFileAsync_SuccessfulProcessing_PersiststTransactionsAndUpdatesStatus()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var s3Key = "cnab/file.txt";
        var fileName = "file.txt";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);

        _fileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId))
            .ReturnsAsync(file);

        // Setup parser to return valid lines
        var validationResult = new CNABValidationResult
        {
            IsValid = true,
            ValidLines = new()
            {
                new CNABLineData
                {
                    Type = 3, // Income type
                    Date = DateTime.Now,
                    Amount = 100000,
                    CPF = "12345678901",
                    Card = "123456789012",
                    Time = new TimeSpan(10, 30, 0),
                    StoreOwner = "Store Owner",
                    StoreName = "Store Name"
                }
            }
        };

        using var fileStream = new MemoryStream();
        _storageServiceMock
            .Setup(x => x.DownloadAsync(s3Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        _parserMock
            .Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Setup validator to accept all records as valid
        _validatorMock
            .Setup(x => x.ValidateRecord(It.IsAny<CNABLineData>(), It.IsAny<int>()))
            .Returns(new CNABValidationResult { IsValid = true });

        _storeRepositoryMock
            .Setup(x => x.GetByNameAndOwnerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Store?)null);

        _transactionRepositoryMock
            .Setup(x => x.GetByFileIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<Transaction>());

        // Act
        var result = await _service.ProcessFileAsync(fileId, s3Key, fileName, correlationId);

        // Assert
        result.Success.Should().BeTrue();
        result.TransactionsInserted.Should().Be(1);
        result.StoresUpserted.Should().Be(1);

        // Verify transactions were persisted
        _transactionRepositoryMock.Verify(
            x => x.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>()),
            Times.Once);

        // Verify file status was updated to Processed
        _fileRepositoryMock.Verify(
            x => x.UpdateAsync(It.Is<FileEntity>(f => f.StatusCode == FileStatusCode.Processed)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessFileAsync_ProcessingError_ThrowsException()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var s3Key = "cnab/file.txt";
        var fileName = "file.txt";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);

        _fileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId))
            .ReturnsAsync(file);

        // Setup storage to throw error
        _storageServiceMock
            .Setup(x => x.DownloadAsync(s3Key, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("S3 connection failed"));

        // Act & Assert
        // The service propagates the exception rather than catching it
        await Assert.ThrowsAsync<IOException>(async () =>
            await _service.ProcessFileAsync(fileId, s3Key, fileName, correlationId));
    }

    [Fact]
    public async Task ProcessFileAsync_MultipleStoresAndTransactions_GroupsByStoreCorrectly()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var s3Key = "cnab/file.txt";
        var fileName = "file.txt";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);

        _fileRepositoryMock
            .Setup(x => x.GetByIdAsync(fileId))
            .ReturnsAsync(file);

        // Setup parser to return lines for multiple stores
        var validationResult = new CNABValidationResult
        {
            IsValid = true,
            ValidLines = new()
            {
                new CNABLineData
                {
                    Type = 3, // Income
                    Date = DateTime.Now,
                    Amount = 100000,
                    CPF = "11111111111",
                    Card = "111111111111",
                    Time = new TimeSpan(10, 0, 0),
                    StoreOwner = "Owner A",
                    StoreName = "Store A"
                },
                new CNABLineData
                {
                    Type = 4, // Expense
                    Date = DateTime.Now,
                    Amount = 50000,
                    CPF = "22222222222",
                    Card = "222222222222",
                    Time = new TimeSpan(11, 0, 0),
                    StoreOwner = "Owner B",
                    StoreName = "Store B"
                }
            }
        };

        using var fileStream = new MemoryStream();
        _storageServiceMock
            .Setup(x => x.DownloadAsync(s3Key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileStream);

        _parserMock
            .Setup(x => x.ParseAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Setup validator to accept all records as valid
        _validatorMock
            .Setup(x => x.ValidateRecord(It.IsAny<CNABLineData>(), It.IsAny<int>()))
            .Returns(new CNABValidationResult { IsValid = true });

        _storeRepositoryMock
            .Setup(x => x.GetByNameAndOwnerAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Store?)null);

        _transactionRepositoryMock
            .Setup(x => x.GetByFileIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<Transaction>());

        // Act
        var result = await _service.ProcessFileAsync(fileId, s3Key, fileName, correlationId);

        // Assert
        result.Success.Should().BeTrue();
        result.StoresUpserted.Should().Be(2); // Two different stores
        result.TransactionsInserted.Should().Be(2); // Two transactions
    }
}
