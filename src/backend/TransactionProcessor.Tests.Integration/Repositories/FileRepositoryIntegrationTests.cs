using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Repositories;
using TransactionProcessor.Tests.Integration.Fixtures;
using Xunit;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for FileRepository using PostgreSQL Testcontainers.
/// 
/// Tests verify:
/// - CRUD operations for File aggregate
/// - Status transitions (Uploaded → Processing → Processed/Rejected)
/// - GetPendingFilesAsync for worker queue processing
/// - Eager loading of Transactions with Store and TransactionType
/// - Unique constraint on S3Key
/// </summary>
[Collection("RepositoryIntegration")]
public class FileRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly RepositoryIntegrationFixture _fixture;

    public FileRepositoryIntegrationTests()
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
    public async Task AddAsync_ValidFile_PersistsToDatabase()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();
        var file = RepositoryIntegrationFixture.CreateFile(
            fileName: "cnab_20260120.txt",
            fileSize: 2048
        );

        // Act
        await repository.AddAsync(file);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Files
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == file.Id);

        persisted.Should().NotBeNull();
        persisted!.FileName.Should().Be("cnab_20260120.txt");
        persisted.FileSize.Should().Be(2048);
        persisted.StatusCode.Should().Be(FileStatusCode.Uploaded);
        persisted.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        persisted.ProcessedAt.Should().BeNull();
        persisted.ErrorMessage.Should().BeNull();

        await context.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_NullFile_ThrowsArgumentNullException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();

        // Act
        Func<Task> act = () => repository.AddAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("file");

        await context.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_DuplicateS3Key_ThrowsDbUpdateException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();
        var s3Key = $"cnab/unique_{Guid.NewGuid():N}.txt";
        
        var file1 = RepositoryIntegrationFixture.CreateFile("file1.txt");
        file1.S3Key = s3Key;
        await repository.AddAsync(file1);

        var file2 = RepositoryIntegrationFixture.CreateFile("file2.txt");
        file2.S3Key = s3Key; // Same S3 key

        // Act
        var (repository2, context2) = _fixture.CreateFileRepository();
        Func<Task> act = () => repository2.AddAsync(file2);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();

        await context.DisposeAsync();
        await context2.DisposeAsync();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingFile_ReturnsFileWithTransactions()
    {
        // Arrange - Create file, store, and transactions
        await using var setupContext = _fixture.CreateDbContext();
        
        var store = RepositoryIntegrationFixture.CreateStore();
        await setupContext.Stores.AddAsync(store);
        
        var file = RepositoryIntegrationFixture.CreateFile("file_with_transactions.txt");
        file.StartProcessing();
        file.MarkAsProcessed();
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        var transactions = new List<Transaction>
        {
            RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "4", 10000m),
            RepositoryIntegrationFixture.CreateTransaction(file.Id, store.Id, "1", 5000m),
        };
        await setupContext.Transactions.AddRangeAsync(transactions);
        await setupContext.SaveChangesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = await repository.GetByIdAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(file.Id);
        result.FileName.Should().Be("file_with_transactions.txt");
        result.StatusCode.Should().Be(FileStatusCode.Processed);
        
        // Verify eager loading of Transactions and Store
        result.Transactions.Should().HaveCount(2);
        result.Transactions.Should().OnlyContain(t => t.Store != null);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingFile_ReturnsNull()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();

        // Act
        var result = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();

        await context.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_FileWithNoTransactions_ReturnsFileWithEmptyCollection()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();
        var file = RepositoryIntegrationFixture.CreateFile("empty_file.txt");
        await repository.AddAsync(file);

        // Act
        var (readRepository, readContext) = _fixture.CreateFileRepository();
        var result = await readRepository.GetByIdAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Transactions.Should().BeEmpty();

        await context.DisposeAsync();
        await readContext.DisposeAsync();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_MultipleFiles_ReturnsAllOrderedByUploadedAtDescending()
    {
        // Arrange
        await _fixture.ClearEntityTablesAsync();

        await using var setupContext = _fixture.CreateDbContext();
        var files = new List<FileEntity>();
        
        for (int i = 0; i < 5; i++)
        {
            var file = RepositoryIntegrationFixture.CreateFile($"file_{i}.txt");
            // Simulate different upload times
            await Task.Delay(10);
            files.Add(file);
        }
        
        await setupContext.Files.AddRangeAsync(files);
        await setupContext.SaveChangesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = (await repository.GetAllAsync()).ToList();

        // Assert
        result.Should().HaveCount(5);
        
        // Verify descending order by UploadedAt (most recent first)
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].UploadedAt.Should().BeOnOrAfter(result[i + 1].UploadedAt);
        }

        await context.DisposeAsync();
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Arrange
        await _fixture.ClearEntityTablesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = await repository.GetAllAsync();

        // Assert
        result.Should().BeEmpty();

        await context.DisposeAsync();
    }

    #endregion

    #region GetPendingFilesAsync Tests

    [Fact]
    public async Task GetPendingFilesAsync_MixedStatuses_ReturnsOnlyUploadedFiles()
    {
        // Arrange
        await _fixture.ClearEntityTablesAsync();

        await using var setupContext = _fixture.CreateDbContext();
        
        // Create files with different statuses
        var uploadedFile1 = RepositoryIntegrationFixture.CreateFile("uploaded1.txt");
        var uploadedFile2 = RepositoryIntegrationFixture.CreateFile("uploaded2.txt");
        
        var processingFile = RepositoryIntegrationFixture.CreateFile("processing.txt");
        processingFile.StartProcessing();
        
        var processedFile = RepositoryIntegrationFixture.CreateFile("processed.txt");
        processedFile.StartProcessing();
        processedFile.MarkAsProcessed();
        
        var rejectedFile = RepositoryIntegrationFixture.CreateFile("rejected.txt");
        rejectedFile.StartProcessing();
        rejectedFile.MarkAsRejected("Invalid format");

        await setupContext.Files.AddRangeAsync(
            uploadedFile1, uploadedFile2, processingFile, processedFile, rejectedFile);
        await setupContext.SaveChangesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = (await repository.GetPendingFilesAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(f => f.StatusCode == FileStatusCode.Uploaded);
        result.Should().Contain(f => f.Id == uploadedFile1.Id);
        result.Should().Contain(f => f.Id == uploadedFile2.Id);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task GetPendingFilesAsync_OrderedByUploadedAt_FIFOProcessing()
    {
        // Arrange
        await _fixture.ClearEntityTablesAsync();

        await using var setupContext = _fixture.CreateDbContext();
        var files = new List<FileEntity>();
        
        for (int i = 0; i < 3; i++)
        {
            var file = RepositoryIntegrationFixture.CreateFile($"pending_{i}.txt");
            files.Add(file);
            await setupContext.Files.AddAsync(file);
            await setupContext.SaveChangesAsync();
            await Task.Delay(50); // Ensure different timestamps
        }

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = (await repository.GetPendingFilesAsync()).ToList();

        // Assert
        result.Should().HaveCount(3);
        
        // Verify ascending order by UploadedAt (FIFO - oldest first)
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].UploadedAt.Should().BeOnOrBefore(result[i + 1].UploadedAt);
        }

        await context.DisposeAsync();
    }

    [Fact]
    public async Task GetPendingFilesAsync_NoPendingFiles_ReturnsEmptyCollection()
    {
        // Arrange
        await _fixture.ClearEntityTablesAsync();

        await using var setupContext = _fixture.CreateDbContext();
        
        // Create only non-pending files
        var processedFile = RepositoryIntegrationFixture.CreateFile("processed.txt");
        processedFile.StartProcessing();
        processedFile.MarkAsProcessed();
        
        await setupContext.Files.AddAsync(processedFile);
        await setupContext.SaveChangesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = await repository.GetPendingFilesAsync();

        // Assert
        result.Should().BeEmpty();

        await context.DisposeAsync();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_StatusTransition_UploadedToProcessing()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();
        var file = RepositoryIntegrationFixture.CreateFile("transition_test.txt");
        await repository.AddAsync(file);

        // Act
        var (updateRepository, updateContext) = _fixture.CreateFileRepository();
        var fileToUpdate = await updateContext.Files.FirstAsync(f => f.Id == file.Id);
        fileToUpdate.StartProcessing();
        await updateRepository.UpdateAsync(fileToUpdate);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Files
            .AsNoTracking()
            .FirstAsync(f => f.Id == file.Id);

        persisted.StatusCode.Should().Be(FileStatusCode.Processing);
        persisted.ProcessedAt.Should().BeNull();

        await context.DisposeAsync();
        await updateContext.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_StatusTransition_ProcessingToProcessed()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var file = RepositoryIntegrationFixture.CreateFile("success_test.txt");
        file.StartProcessing();
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Act
        var (updateRepository, updateContext) = _fixture.CreateFileRepository();
        var fileToUpdate = await updateContext.Files.FirstAsync(f => f.Id == file.Id);
        fileToUpdate.MarkAsProcessed();
        await updateRepository.UpdateAsync(fileToUpdate);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Files
            .AsNoTracking()
            .FirstAsync(f => f.Id == file.Id);

        persisted.StatusCode.Should().Be(FileStatusCode.Processed);
        persisted.ProcessedAt.Should().NotBeNull();
        persisted.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        persisted.ErrorMessage.Should().BeNull();

        await updateContext.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_StatusTransition_ProcessingToRejected()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var file = RepositoryIntegrationFixture.CreateFile("rejected_test.txt");
        file.StartProcessing();
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Act
        var (updateRepository, updateContext) = _fixture.CreateFileRepository();
        var fileToUpdate = await updateContext.Files.FirstAsync(f => f.Id == file.Id);
        var errorMessage = "CNAB validation failed: Line 15 has invalid type code X";
        fileToUpdate.MarkAsRejected(errorMessage);
        await updateRepository.UpdateAsync(fileToUpdate);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persisted = await verifyContext.Files
            .AsNoTracking()
            .FirstAsync(f => f.Id == file.Id);

        persisted.StatusCode.Should().Be(FileStatusCode.Rejected);
        persisted.ProcessedAt.Should().NotBeNull();
        persisted.ErrorMessage.Should().Be(errorMessage);

        await updateContext.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_NullFile_ThrowsArgumentNullException()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();

        // Act
        Func<Task> act = () => repository.UpdateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("file");

        await context.DisposeAsync();
    }

    #endregion

    #region GetQueryable Tests

    [Fact]
    public async Task GetQueryable_AllowsCustomFiltering()
    {
        // Arrange
        await _fixture.ClearEntityTablesAsync();

        await using var setupContext = _fixture.CreateDbContext();
        
        var processedFiles = Enumerable.Range(1, 5)
            .Select(i =>
            {
                var f = RepositoryIntegrationFixture.CreateFile($"processed_{i}.txt");
                f.StartProcessing();
                f.MarkAsProcessed();
                return f;
            });
        
        var uploadedFiles = Enumerable.Range(1, 3)
            .Select(i => RepositoryIntegrationFixture.CreateFile($"uploaded_{i}.txt"));

        await setupContext.Files.AddRangeAsync(processedFiles);
        await setupContext.Files.AddRangeAsync(uploadedFiles);
        await setupContext.SaveChangesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = await repository.GetQueryable()
            .AsNoTracking()
            .Where(f => f.StatusCode == FileStatusCode.Processed)
            .ToListAsync();

        // Assert
        result.Should().HaveCount(5);
        result.Should().OnlyContain(f => f.StatusCode == FileStatusCode.Processed);

        await context.DisposeAsync();
    }

    [Fact]
    public async Task GetQueryable_AllowsProjection()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();
        var file = RepositoryIntegrationFixture.CreateFile("projection_test.txt");
        file.FileSize = 5000;
        await repository.AddAsync(file);

        // Act
        var (readRepository, readContext) = _fixture.CreateFileRepository();
        var projection = await readRepository.GetQueryable()
            .AsNoTracking()
            .Where(f => f.Id == file.Id)
            .Select(f => new { f.Id, f.FileName, f.FileSize })
            .FirstOrDefaultAsync();

        // Assert
        projection.Should().NotBeNull();
        projection!.Id.Should().Be(file.Id);
        projection.FileName.Should().Be("projection_test.txt");
        projection.FileSize.Should().Be(5000);

        await context.DisposeAsync();
        await readContext.DisposeAsync();
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task AddAsync_ConcurrentInserts_AllSucceed()
    {
        // Arrange
        var files = Enumerable.Range(1, 10)
            .Select(i => RepositoryIntegrationFixture.CreateFile($"concurrent_{i}.txt"))
            .ToList();

        // Act - Insert concurrently
        var tasks = files.Select(async file =>
        {
            var (repository, context) = _fixture.CreateFileRepository();
            try
            {
                await repository.AddAsync(file);
            }
            finally
            {
                await context.DisposeAsync();
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        await using var verifyContext = _fixture.CreateDbContext();
        var persistedCount = await verifyContext.Files
            .CountAsync(f => f.FileName.StartsWith("concurrent_"));

        persistedCount.Should().Be(10);
    }

    #endregion

    #region Terminal State Tests

    [Fact]
    public async Task IsInTerminalState_ProcessedFile_ReturnsTrue()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var file = RepositoryIntegrationFixture.CreateFile("terminal_processed.txt");
        file.StartProcessing();
        file.MarkAsProcessed();
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = await repository.GetByIdAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsInTerminalState().Should().BeTrue();

        await context.DisposeAsync();
    }

    [Fact]
    public async Task IsInTerminalState_RejectedFile_ReturnsTrue()
    {
        // Arrange
        await using var setupContext = _fixture.CreateDbContext();
        var file = RepositoryIntegrationFixture.CreateFile("terminal_rejected.txt");
        file.StartProcessing();
        file.MarkAsRejected("Test rejection");
        await setupContext.Files.AddAsync(file);
        await setupContext.SaveChangesAsync();

        // Act
        var (repository, context) = _fixture.CreateFileRepository();
        var result = await repository.GetByIdAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsInTerminalState().Should().BeTrue();

        await context.DisposeAsync();
    }

    [Fact]
    public async Task IsInTerminalState_UploadedFile_ReturnsFalse()
    {
        // Arrange
        var (repository, context) = _fixture.CreateFileRepository();
        var file = RepositoryIntegrationFixture.CreateFile("non_terminal_uploaded.txt");
        await repository.AddAsync(file);

        // Act
        var (readRepository, readContext) = _fixture.CreateFileRepository();
        var result = await readRepository.GetByIdAsync(file.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsInTerminalState().Should().BeFalse();

        await context.DisposeAsync();
        await readContext.DisposeAsync();
    }

    #endregion
}
