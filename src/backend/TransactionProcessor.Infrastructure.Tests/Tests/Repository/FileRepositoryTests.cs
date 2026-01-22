using System.Linq;
using FluentAssertions;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Repositories;
using System;
using System.Threading.Tasks;
using Xunit;
using FileEntity = TransactionProcessor.Domain.Entities.File;
using Task = System.Threading.Tasks.Task;

namespace TransactionProcessor.Infrastructure.Tests.Tests.Repository;

/// <summary>
/// Repository integration tests for File entity.
/// Tests create, status updates, and status-based queries against PostgreSQL.
/// </summary>
public class FileRepositoryTests : IntegrationTestBase
{
    private FileRepository CreateRepository() => new(DbContext);

    [Fact]
    public async Task AddAsync_Should_Persist_File_Successfully()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "test-upload.txt");

        // Act
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await repository.GetByIdAsync(file.Id);
        persisted.Should().NotBeNull();
        persisted?.FileName.Should().Be("test-upload.txt");
        persisted?.StatusCode.Should().Be(FileStatusCode.Uploaded);
        persisted?.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_File_With_Transactions()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "file-with-transactions.txt");
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await repository.GetByIdAsync(file.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved?.Transactions.Should().NotBeNull();
        retrieved?.Transactions.Should().BeEmpty(); // No transactions yet
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_All_Files_Ordered_By_UploadedAt()
    {
        // Arrange
        var repository = CreateRepository();
        
        var file1 = new FileEntity(Guid.NewGuid(), "first.txt");
        await Task.Delay(10); // Ensure different timestamps
        var file2 = new FileEntity(Guid.NewGuid(), "second.txt");
        
        await repository.AddAsync(file1);
        await repository.AddAsync(file2);
        await DbContext.SaveChangesAsync();

        // Act
        var files = await repository.GetAllAsync();

        // Assert
        files.Should().HaveCountGreaterOrEqualTo(2);
        files.Should().Contain(f => f.FileName == "first.txt");
        files.Should().Contain(f => f.FileName == "second.txt");
        
        // Should be ordered by UploadedAt descending (newest first)
        var fileList = files.ToList();
        fileList.Should().BeInDescendingOrder(f => f.UploadedAt);
    }

    [Fact]
    public async Task GetPendingFilesAsync_Should_Return_Only_Uploaded_Status()
    {
        // Arrange
        var repository = CreateRepository();
        
        var uploadedFile = new FileEntity(Guid.NewGuid(), "uploaded.txt");
        var processingFile = new FileEntity(Guid.NewGuid(), "processing.txt");
        processingFile.StartProcessing();
        
        var processedFile = new FileEntity(Guid.NewGuid(), "processed.txt");
        processedFile.StartProcessing();
        processedFile.MarkAsProcessed();

        await repository.AddAsync(uploadedFile);
        await repository.AddAsync(processingFile);
        await repository.AddAsync(processedFile);
        await DbContext.SaveChangesAsync();

        // Act
        var pendingFiles = await repository.GetPendingFilesAsync();

        // Assert
        pendingFiles.Should().HaveCount(1);
        pendingFiles.First().FileName.Should().Be("uploaded.txt");
        pendingFiles.First().StatusCode.Should().Be(FileStatusCode.Uploaded);
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_File_Status_To_Processing()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "to-process.txt");
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Act - Update status to Processing
        file.StartProcessing();
        await repository.UpdateAsync(file);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await repository.GetByIdAsync(file.Id);
        updated.Should().NotBeNull();
        updated?.StatusCode.Should().Be(FileStatusCode.Processing);
        updated?.ProcessedAt.Should().BeNull(); // Not processed yet
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_File_Status_To_Processed()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "complete-process.txt");
        file.StartProcessing();
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Act - Mark as processed
        file.MarkAsProcessed();
        await repository.UpdateAsync(file);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await repository.GetByIdAsync(file.Id);
        updated.Should().NotBeNull();
        updated?.StatusCode.Should().Be(FileStatusCode.Processed);
        updated?.ProcessedAt.Should().NotBeNull();
        updated?.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateAsync_Should_Update_File_Status_To_Rejected_With_Error()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "invalid-file.txt");
        file.StartProcessing();
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Act - Reject with error message
        var errorMessage = "Invalid CNAB format: Missing required fields";
        file.MarkAsRejected(errorMessage);
        await repository.UpdateAsync(file);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await repository.GetByIdAsync(file.Id);
        updated.Should().NotBeNull();
        updated?.StatusCode.Should().Be(FileStatusCode.Rejected);
        updated?.ErrorMessage.Should().Be(errorMessage);
        updated?.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_Remove_File_From_Database()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "to-delete.txt");
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Act
        DbContext.Files.Remove(file);
        await DbContext.SaveChangesAsync();

        // Assert
        var deleted = await repository.GetByIdAsync(file.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task File_Status_Workflow_Should_Transition_Correctly()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "workflow-test.txt");
        
        // Act & Assert - Initial state
        file.StatusCode.Should().Be(FileStatusCode.Uploaded);
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Transition to Processing
        file.StartProcessing();
        await repository.UpdateAsync(file);
        await DbContext.SaveChangesAsync();
        
        var processing = await repository.GetByIdAsync(file.Id);
        processing?.StatusCode.Should().Be(FileStatusCode.Processing);

        // Transition to Processed
        file.MarkAsProcessed();
        await repository.UpdateAsync(file);
        await DbContext.SaveChangesAsync();
        
        var processed = await repository.GetByIdAsync(file.Id);
        processed?.StatusCode.Should().Be(FileStatusCode.Processed);
        processed?.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPendingFilesAsync_Should_Return_Files_In_FIFO_Order()
    {
        // Arrange
        var repository = CreateRepository();
        
        var file1 = new FileEntity(Guid.NewGuid(), "first-upload.txt");
        await Task.Delay(20); // Ensure different timestamps
        var file2 = new FileEntity(Guid.NewGuid(), "second-upload.txt");
        await Task.Delay(20);
        var file3 = new FileEntity(Guid.NewGuid(), "third-upload.txt");

        await repository.AddAsync(file1);
        await repository.AddAsync(file2);
        await repository.AddAsync(file3);
        await DbContext.SaveChangesAsync();

        // Act
        var pendingFiles = await repository.GetPendingFilesAsync();

        // Assert
        var fileList = pendingFiles.ToList();
        fileList.Should().HaveCountGreaterOrEqualTo(3);
        
        // Should be ordered by UploadedAt ascending (FIFO)
        fileList.Should().BeInAscendingOrder(f => f.UploadedAt);
    }

    [Fact]
    public async Task Multiple_Status_Transitions_Should_Maintain_Data_Integrity()
    {
        // Arrange
        var repository = CreateRepository();
        var file = new FileEntity(Guid.NewGuid(), "multi-status-test.txt");
        await repository.AddAsync(file);
        await DbContext.SaveChangesAsync();

        // Act - Transition through multiple states
        file.StartProcessing();
        await repository.UpdateAsync(file);
        await DbContext.SaveChangesAsync();

        file.MarkAsRejected("Test error");
        await repository.UpdateAsync(file);
        await DbContext.SaveChangesAsync();

        // Assert
        var final = await repository.GetByIdAsync(file.Id);
        final.Should().NotBeNull();
        final?.StatusCode.Should().Be(FileStatusCode.Rejected);
        final?.ErrorMessage.Should().Be("Test error");
        final?.FileName.Should().Be("multi-status-test.txt");
        final?.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }
}
