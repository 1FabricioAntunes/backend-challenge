using FluentAssertions;
using System;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Domain.UnitTests.Helpers;
using Xunit;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Domain.UnitTests.Tests.Entities;

/// <summary>
/// Unit tests for File aggregate entity.
/// Tests file state machine transitions and invariants.
/// </summary>
public class FileTests : TestBase
{
    [Fact]
    public void Constructor_ValidFileName_CreatesFileWithUploadedStatus()
    {
        // Arrange
        var id = NewGuid();
        var fileName = "test-cnab.txt";

        // Act
        FileEntity file = new FileEntity(id, fileName);

        // Assert
        file.Id.Should().Be(id);
        file.FileName.Should().Be(fileName);
        file.StatusCode.Should().Be(FileStatusCode.Uploaded);
        file.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        file.ProcessedAt.Should().BeNull();
        file.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidFileName_ThrowsArgumentException(string? fileName)
    {
        // Act
        Action act = () => new FileEntity(NewGuid(), fileName!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StartProcessing_FromUploaded_TransitionsToProcessing()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");

        // Act
        file.StartProcessing();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processing);
    }

    [Fact]
    public void StartProcessing_NotFromUploaded_ThrowsInvalidOperationException()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");
        file.StartProcessing();

        // Act
        Action act = () => file.StartProcessing();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsProcessed_FromProcessing_TransitionsToProcessed()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");
        file.StartProcessing();

        // Act
        file.MarkAsProcessed();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.ProcessedAt.Should().NotBeNull();
        file.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsProcessed_NotFromProcessing_ThrowsInvalidOperationException()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");

        // Act
        Action act = () => file.MarkAsProcessed();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsRejected_FromProcessing_TransitionsToRejected()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");
        file.StartProcessing();
        var errorMessage = "Invalid CNAB format";

        // Act
        file.MarkAsRejected(errorMessage);

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.ProcessedAt.Should().NotBeNull();
        file.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void MarkAsRejected_InvalidErrorMessage_ThrowsArgumentException()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");
        file.StartProcessing();

        // Act
        Action act = () => file.MarkAsRejected(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsRejected_LongErrorMessage_TruncatesToMaxLength()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");
        file.StartProcessing();
        var longError = new string('x', 2000); // Longer than max

        // Act
        file.MarkAsRejected(longError);

        // Assert
        file.ErrorMessage!.Length.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public void FileLifecycle_SuccessfulProcessing_CompletesCorrectly()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");

        // Act - Upload phase
        file.StatusCode.Should().Be(FileStatusCode.Uploaded);

        // Act - Processing phase
        file.StartProcessing();
        file.StatusCode.Should().Be(FileStatusCode.Processing);

        // Act - Complete phase
        file.MarkAsProcessed();
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void FileLifecycle_ProcessingWithError_CompletesWithRejection()
    {
        // Arrange
        FileEntity file = new FileEntity(NewGuid(), "test.txt");

        // Act
        file.StartProcessing();
        file.MarkAsRejected("Validation failed");

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.ProcessedAt.Should().NotBeNull();
        file.ErrorMessage.Should().Be("Validation failed");
    }
}
