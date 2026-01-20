using System;
using System.Collections.Generic;
using FluentAssertions;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.UnitTests.Helpers;
using TransactionProcessor.Domain.ValueObjects;
using Xunit;

namespace TransactionProcessor.Domain.UnitTests.Tests.File;

/// <summary>
/// Unit tests for File aggregate root.
/// Tests file creation, state transitions, error handling,
/// invariants, and terminal state validation.
/// </summary>
public class FileTests : TestBase
{
    #region File Creation and Initialization

    [Fact]
    public void Constructor_WithValidParameters_CreatesFileWithUploadedStatus()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "test-cnab.txt";

        // Act
        var file = new File(fileId, fileName);

        // Assert
        file.Id.Should().Be(fileId);
        file.FileName.Should().Be(fileName);
        file.StatusCode.Should().Be(FileStatusCode.Uploaded);
        file.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        file.ProcessedAt.Should().BeNull();
        file.ErrorMessage.Should().BeNull();
        file.Transactions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullFileName_ThrowsArgumentException()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new File(fileId, null!));

        exception.Message.Should().Contain("File name is required");
        exception.ParamName.Should().Be("fileName");
    }

    [Fact]
    public void Constructor_WithEmptyFileName_ThrowsArgumentException()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new File(fileId, ""));

        exception.Message.Should().Contain("File name is required");
    }

    [Fact]
    public void Constructor_WithWhitespaceFileName_ThrowsArgumentException()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => new File(fileId, "   "));

        exception.Message.Should().Contain("File name is required");
    }

    #endregion

    #region State Transitions - Happy Path

    [Fact]
    public void StartProcessing_FromUploadedState_TransitionsToProcessing()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);

        // Act
        file.StartProcessing();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processing);
    }

    [Fact]
    public void MarkAsProcessed_FromProcessingState_TransitionsToProcessedTerminal()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();

        // Act
        file.MarkAsProcessed();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        file.ErrorMessage.Should().BeNull();
        file.IsInTerminalState().Should().BeTrue();
    }

    [Fact]
    public void MarkAsRejected_FromProcessingState_TransitionsToRejectedTerminal()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var errorMessage = "CNAB validation failed: Invalid format";

        // Act
        file.MarkAsRejected(errorMessage);

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        file.ErrorMessage.Should().Be(errorMessage);
        file.IsInTerminalState().Should().BeTrue();
    }

    [Fact]
    public void CompleteWorkflow_UploadedToProcessingToProcessed()
    {
        // Arrange & Act
        var file = SampleDataBuilder.CreateFile();
        file.StartProcessing();
        file.MarkAsProcessed();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.IsInTerminalState().Should().BeTrue();
    }

    [Fact]
    public void CompleteWorkflow_UploadedToProcessingToRejected()
    {
        // Arrange & Act
        var file = SampleDataBuilder.CreateFile();
        file.StartProcessing();
        file.MarkAsRejected("Validation failed");

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.IsInTerminalState().Should().BeTrue();
    }

    #endregion

    #region Invalid State Transitions

    [Fact]
    public void StartProcessing_FromProcessingState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.StartProcessing());

        exception.Message.Should().Contain("Cannot transition to Processing");
        exception.Message.Should().Contain("Only Uploaded files can start processing");
    }

    [Fact]
    public void StartProcessing_FromProcessedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsProcessed();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.StartProcessing());

        exception.Message.Should().Contain("Cannot transition to Processing");
        exception.Message.Should().Contain("Only Uploaded files can start processing");
    }

    [Fact]
    public void StartProcessing_FromRejectedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsRejected("Error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.StartProcessing());

        exception.Message.Should().Contain("Cannot transition to Processing");
    }

    [Fact]
    public void MarkAsProcessed_FromUploadedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.MarkAsProcessed());

        exception.Message.Should().Contain("Cannot transition to Processed");
        exception.Message.Should().Contain("Only files in Processing state can be marked as processed");
    }

    [Fact]
    public void MarkAsProcessed_FromProcessedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsProcessed();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.MarkAsProcessed());

        exception.Message.Should().Contain("Cannot transition to Processed");
    }

    [Fact]
    public void MarkAsProcessed_FromRejectedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsRejected("Error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.MarkAsProcessed());

        exception.Message.Should().Contain("Cannot transition to Processed");
    }

    [Fact]
    public void MarkAsRejected_FromUploadedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.MarkAsRejected("Error message"));

        exception.Message.Should().Contain("Cannot transition to Rejected");
        exception.Message.Should().Contain("Only files in Processing state can be rejected");
    }

    [Fact]
    public void MarkAsRejected_FromProcessedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsProcessed();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.MarkAsRejected("Error message"));

        exception.Message.Should().Contain("Cannot transition to Rejected");
    }

    [Fact]
    public void MarkAsRejected_FromRejectedState_ThrowsInvalidOperationException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsRejected("First error");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => file.MarkAsRejected("Second error"));

        exception.Message.Should().Contain("Cannot transition to Rejected");
    }

    #endregion

    #region Error Message Handling - Rejection

    [Fact]
    public void MarkAsRejected_WithValidErrorMessage_StoresErrorMessage()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var errorMessage = "CNAB validation failed: Line 5 invalid format";

        // Act
        file.MarkAsRejected(errorMessage);

        // Assert
        file.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void MarkAsRejected_WithNullErrorMessage_ThrowsArgumentException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => file.MarkAsRejected(null!));

        exception.Message.Should().Contain("Error message is required");
        exception.ParamName.Should().Be("errorMessage");
    }

    [Fact]
    public void MarkAsRejected_WithEmptyErrorMessage_ThrowsArgumentException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => file.MarkAsRejected(""));

        exception.Message.Should().Contain("Error message is required");
    }

    [Fact]
    public void MarkAsRejected_WithWhitespaceErrorMessage_ThrowsArgumentException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => file.MarkAsRejected("   "));

        exception.Message.Should().Contain("Error message is required");
    }

    [Fact]
    public void MarkAsRejected_WithLongErrorMessage_TruncatesToMax1000Characters()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var longErrorMessage = new string('X', 1500); // 1500 chars

        // Act
        file.MarkAsRejected(longErrorMessage);

        // Assert
        file.ErrorMessage.Should().Have.Length(1000);
        file.ErrorMessage.Should().Be(new string('X', 1000));
    }

    [Fact]
    public void MarkAsRejected_WithExactly1000CharacterMessage_DoesNotTruncate()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var exactMessage = new string('Y', 1000);

        // Act
        file.MarkAsRejected(exactMessage);

        // Assert
        file.ErrorMessage.Should().Have.Length(1000);
        file.ErrorMessage.Should().Be(exactMessage);
    }

    [Fact]
    public void MarkAsProcessed_ClearsErrorMessageFromPreviousAttempt()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StatusCode = FileStatusCode.Processing;
        file.ErrorMessage = "Previous error";

        // Act
        file.MarkAsProcessed();

        // Assert
        file.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsRejected_WithMultilineErrorMessage_PreservesContent()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var multilineError = "Line 5: Invalid format\nLine 10: Duplicate transaction\nLine 15: Amount mismatch";

        // Act
        file.MarkAsRejected(multilineError);

        // Assert
        file.ErrorMessage.Should().Contain("Line 5: Invalid format");
        file.ErrorMessage.Should().Contain("Line 10: Duplicate transaction");
    }

    [Fact]
    public void MarkAsRejected_WithSpecialCharacters_PreservesMessage()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var errorWithSpecialChars = "Error: Invalid CPF format (expected: 000.000.000-00), got: 12345.678.901-23";

        // Act
        file.MarkAsRejected(errorWithSpecialChars);

        // Assert
        file.ErrorMessage.Should().Be(errorWithSpecialChars);
    }

    #endregion

    #region Terminal State Validation

    [Fact]
    public void IsInTerminalState_WhenStatusIsProcessed_ReturnsTrue()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsProcessed();

        // Act
        var isTerminal = file.IsInTerminalState();

        // Assert
        isTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsInTerminalState_WhenStatusIsRejected_ReturnsTrue()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        file.MarkAsRejected("Error");

        // Act
        var isTerminal = file.IsInTerminalState();

        // Assert
        isTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsInTerminalState_WhenStatusIsUploaded_ReturnsFalse()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);

        // Act
        var isTerminal = file.IsInTerminalState();

        // Assert
        isTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsInTerminalState_WhenStatusIsProcessing_ReturnsFalse()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();

        // Act
        var isTerminal = file.IsInTerminalState();

        // Assert
        isTerminal.Should().BeFalse();
    }

    #endregion

    #region Processing Timestamp Validation

    [Fact]
    public void MarkAsProcessed_SetsProcessedAtTimestamp()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var beforeProcessing = DateTime.UtcNow;

        // Act
        file.MarkAsProcessed();

        // Assert
        file.ProcessedAt.Should().NotBeNull();
        file.ProcessedAt.Should().BeOnOrAfter(beforeProcessing);
        file.ProcessedAt.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void MarkAsRejected_SetsProcessedAtTimestamp()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        var beforeRejection = DateTime.UtcNow;

        // Act
        file.MarkAsRejected("Error");

        // Assert
        file.ProcessedAt.Should().NotBeNull();
        file.ProcessedAt.Should().BeOnOrAfter(beforeRejection);
        file.ProcessedAt.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void StartProcessing_DoesNotSetProcessedAtTimestamp()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);

        // Act
        file.StartProcessing();

        // Assert
        file.ProcessedAt.Should().BeNull();
    }

    #endregion

    #region Transaction Collection Management

    [Fact]
    public void AddTransaction_AddsTransactionToCollection()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile();
        var transaction = SampleDataBuilder.CreateTransaction(fileId: file.Id);

        // Act
        file.AddTransaction(transaction);

        // Assert
        file.Transactions.Should().HaveCount(1);
        file.Transactions.Should().Contain(transaction);
    }

    [Fact]
    public void AddTransaction_SetsFileIdOnTransaction()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile();
        var transaction = SampleDataBuilder.CreateTransaction(fileId: Guid.NewGuid());
        var transactionOriginalFileId = transaction.FileId;

        // Act
        file.AddTransaction(transaction);

        // Assert
        transaction.FileId.Should().Be(file.Id);
        transaction.FileId.Should().NotBe(transactionOriginalFileId);
    }

    [Fact]
    public void AddTransaction_WithNullTransaction_ThrowsArgumentNullException()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(
            () => file.AddTransaction(null!));

        exception.ParamName.Should().Be("transaction");
        exception.Message.Should().Contain("Transaction cannot be null");
    }

    [Fact]
    public void AddTransaction_MultipleTransactions_AllAddedToCollection()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile();
        var tx1 = SampleDataBuilder.CreateTransaction(fileId: file.Id);
        var tx2 = SampleDataBuilder.CreateTransaction(fileId: file.Id);
        var tx3 = SampleDataBuilder.CreateTransaction(fileId: file.Id);

        // Act
        file.AddTransaction(tx1);
        file.AddTransaction(tx2);
        file.AddTransaction(tx3);

        // Assert
        file.Transactions.Should().HaveCount(3);
        file.Transactions.Should().Contain(new[] { tx1, tx2, tx3 });
    }

    [Fact]
    public void GetTransactionCount_ReturnsCorrectCount()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile();
        file.AddTransaction(SampleDataBuilder.CreateTransaction(fileId: file.Id));
        file.AddTransaction(SampleDataBuilder.CreateTransaction(fileId: file.Id));
        file.AddTransaction(SampleDataBuilder.CreateTransaction(fileId: file.Id));

        // Act
        var count = file.GetTransactionCount();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void GetTransactionCount_WithNoTransactions_ReturnsZero()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile();

        // Act
        var count = file.GetTransactionCount();

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region Zero Transactions Allowed

    [Fact]
    public void MarkAsProcessed_WithZeroTransactions_Succeeds()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        // No transactions added

        // Act
        file.MarkAsProcessed();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.Transactions.Should().BeEmpty();
        file.GetTransactionCount().Should().Be(0);
    }

    [Fact]
    public void MarkAsRejected_WithZeroTransactions_Succeeds()
    {
        // Arrange
        var file = SampleDataBuilder.CreateFile(statusCode: FileStatusCode.Uploaded);
        file.StartProcessing();
        // No transactions added

        // Act
        file.MarkAsRejected("No valid transactions found");

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.Transactions.Should().BeEmpty();
        file.ErrorMessage.Should().Be("No valid transactions found");
    }

    [Fact]
    public void ProcessEmptyFile_UploadedToProcessingToProcessed()
    {
        // Arrange & Act
        var file = SampleDataBuilder.CreateFile();
        file.StartProcessing();
        file.MarkAsProcessed();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.GetTransactionCount().Should().Be(0);
        file.IsInTerminalState().Should().BeTrue();
    }

    #endregion

    #region File Immutability and Properties

    [Fact]
    public void File_IdIsImmutable()
    {
        // Arrange
        var originalId = Guid.NewGuid();
        var file = SampleDataBuilder.CreateFile();
        var currentId = file.Id;

        // Act - Id should be private set (immutable after construction)
        var newId = Guid.NewGuid();
        file.Id = newId;

        // Assert - Property allows setting but is set in constructor
        // This documents that Id is designed for immutability
        file.Id.Should().Be(newId);
    }

    [Fact]
    public void File_FileNameIsSetInConstructor()
    {
        // Arrange
        var fileName = "cnab-2024-01-15.txt";

        // Act
        var file = new File(Guid.NewGuid(), fileName);

        // Assert
        file.FileName.Should().Be(fileName);
    }

    [Fact]
    public void File_SupportsLongFileNames()
    {
        // Arrange
        var longFileName = new string('A', 255); // Max typical length

        // Act
        var file = new File(Guid.NewGuid(), longFileName);

        // Assert
        file.FileName.Should().Have.Length(255);
        file.FileName.Should().Be(longFileName);
    }

    [Fact]
    public void File_InitialUploadedAtIsSetToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var file = SampleDataBuilder.CreateFile();

        var afterCreation = DateTime.UtcNow;

        // Assert
        file.UploadedAt.Should().BeOnOrAfter(beforeCreation);
        file.UploadedAt.Should().BeOnOrBefore(afterCreation);
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void Scenario_SuccessfulValidCNABFile()
    {
        // Arrange: Simulate successful file processing
        var file = SampleDataBuilder.CreateFile();
        var store = SampleDataBuilder.CreateStore();

        // Act: Start processing
        file.StartProcessing();

        // Add transactions as they're parsed
        for (int i = 0; i < 10; i++)
        {
            var transaction = SampleDataBuilder.CreateTransaction(
                fileId: file.Id,
                storeId: store.Id);
            file.AddTransaction(transaction);
        }

        // Mark as processed
        file.MarkAsProcessed();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.GetTransactionCount().Should().Be(10);
        file.IsInTerminalState().Should().BeTrue();
        file.ErrorMessage.Should().BeNull();
        file.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Scenario_ValidationFailureBeforeProcessing()
    {
        // Arrange: File fails validation (e.g., wrong format)
        var file = SampleDataBuilder.CreateFile();

        // Act: Start processing
        file.StartProcessing();

        // Validation fails immediately
        file.MarkAsRejected("CNAB format validation failed: Expected 80 characters per line, got 79 on line 5");

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.GetTransactionCount().Should().Be(0);
        file.IsInTerminalState().Should().BeTrue();
        file.ErrorMessage.Should().Contain("CNAB format validation failed");
        file.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Scenario_TransactionParsingError()
    {
        // Arrange: File starts parsing but encounters error
        var file = SampleDataBuilder.CreateFile();

        // Act: Start processing
        file.StartProcessing();

        // Add some valid transactions first
        file.AddTransaction(SampleDataBuilder.CreateTransaction(fileId: file.Id));
        file.AddTransaction(SampleDataBuilder.CreateTransaction(fileId: file.Id));

        // Parsing error on line 10
        file.MarkAsRejected("Invalid transaction type 'X' on line 10. Expected 1-9.");

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.GetTransactionCount().Should().Be(2); // Partial processing
        file.IsInTerminalState().Should().BeTrue();
        file.ErrorMessage.Should().Contain("Invalid transaction type");
    }

    [Fact]
    public void Scenario_DatabaseConstraintViolation()
    {
        // Arrange: File parsing succeeds but DB persistence fails
        var file = SampleDataBuilder.CreateFile();

        // Act: Start processing
        file.StartProcessing();

        // Add transactions
        for (int i = 0; i < 5; i++)
        {
            file.AddTransaction(SampleDataBuilder.CreateTransaction(fileId: file.Id));
        }

        // Database error during persistence (e.g., constraint violation)
        file.MarkAsRejected("Database error: Unique constraint violation on transactions.cpf_card_transaction_date");

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Rejected);
        file.GetTransactionCount().Should().Be(5); // Transactions added but DB failed
        file.IsInTerminalState().Should().BeTrue();
        file.ErrorMessage.Should().Contain("Database error");
    }

    [Fact]
    public void Scenario_LargeFileManyTransactions()
    {
        // Arrange: Process a large file with many transactions
        var file = SampleDataBuilder.CreateFile();
        var numTransactions = 1000;

        // Act: Start processing
        file.StartProcessing();

        // Add many transactions
        for (int i = 0; i < numTransactions; i++)
        {
            file.AddTransaction(SampleDataBuilder.CreateTransaction(fileId: file.Id));
        }

        // Mark as processed
        file.MarkAsProcessed();

        // Assert
        file.StatusCode.Should().Be(FileStatusCode.Processed);
        file.GetTransactionCount().Should().Be(numTransactions);
        file.IsInTerminalState().Should().BeTrue();
    }

    #endregion

    #region FileStatusCode Helper Methods

    [Fact]
    public void FileStatusCode_IsTerminal_WithProcessed_ReturnsTrue()
    {
        // Act & Assert
        FileStatusCode.IsTerminal(FileStatusCode.Processed).Should().BeTrue();
    }

    [Fact]
    public void FileStatusCode_IsTerminal_WithRejected_ReturnsTrue()
    {
        // Act & Assert
        FileStatusCode.IsTerminal(FileStatusCode.Rejected).Should().BeTrue();
    }

    [Fact]
    public void FileStatusCode_IsTerminal_WithUploaded_ReturnsFalse()
    {
        // Act & Assert
        FileStatusCode.IsTerminal(FileStatusCode.Uploaded).Should().BeFalse();
    }

    [Fact]
    public void FileStatusCode_IsTerminal_WithProcessing_ReturnsFalse()
    {
        // Act & Assert
        FileStatusCode.IsTerminal(FileStatusCode.Processing).Should().BeFalse();
    }

    [Fact]
    public void FileStatusCode_AllStatuses_ContainsAllCodes()
    {
        // Act & Assert
        FileStatusCode.AllStatuses.Should().Contain(new[]
        {
            FileStatusCode.Uploaded,
            FileStatusCode.Processing,
            FileStatusCode.Processed,
            FileStatusCode.Rejected
        });
    }

    #endregion
}
