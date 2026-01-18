using TransactionProcessor.Domain.ValueObjects;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents an uploaded CNAB file and its processing lifecycle.
/// Aggregate root for file-related operations and transaction persistence.
/// 
/// Status Lifecycle:
/// - Uploaded: File received and stored in S3, awaiting processing
/// - Processing: File is being validated and transactions are being parsed
/// - Processed: File validation and transaction persistence succeeded (terminal)
/// - Rejected: File validation failed or processing error occurred (terminal)
/// 
/// Invariants:
/// - Id is immutable (set only in constructor)
/// - FileName is required (non-empty)
/// - Status transitions follow the lifecycle above
/// - Once in Processed or Rejected state, no further transitions allowed
/// - ProcessedAt is set only when transitioning to Processed or Rejected
/// - ErrorMessage is set only when transitioning to Rejected
/// - Transactions can only be added in Uploaded state (before processing)
/// 
/// Reference: docs/business-rules.md § File States for detailed state machine
/// </summary>
public class File
{
    /// <summary>
    /// Unique identifier for the file (UUID v7, time-ordered).
    /// Immutable; set only in constructor to enforce aggregate identity.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Original uploaded file name (max 255 characters).
    /// Required; cannot be null or empty.
    /// Used for audit trail and user-facing display.
    /// </summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>
    /// Current processing status code (FK to file_statuses lookup table).
    /// Values: Uploaded, Processing, Processed, Rejected (from FileStatusCode enum).
    /// This is the source of truth for the file's lifecycle stage.
    /// </summary>
    public string StatusCode { get; private set; } = FileStatusCode.Uploaded;

    /// <summary>
    /// File size in bytes (BIGINT) for audit and validation.
    /// Used to track storage usage and validate file size constraints.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// S3 storage key (e.g., cnab/{fileId}/{fileName}).
    /// Unique constraint ensures no duplicate storage.
    /// Used to retrieve file content for processing.
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// User ID who uploaded the file (nullable).
    /// Used for audit trail and access control.
    /// </summary>
    public Guid? UploadedByUserId { get; set; }

    /// <summary>
    /// Timestamp when file was uploaded (UTC).
    /// Set automatically in constructor; immutable after creation.
    /// </summary>
    public DateTime UploadedAt { get; private set; }

    /// <summary>
    /// Timestamp when file processing completed (UTC, nullable).
    /// Set when transitioning to Processed or Rejected state.
    /// Null until processing is complete.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Error message if file processing failed (max 1000 characters).
    /// Set only when status transitions to Rejected.
    /// Contains validation errors or processing exception details.
    /// Null for successfully processed files.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    // Navigation properties
    /// <summary>
    /// Transactions parsed from this CNAB file.
    /// Cascading delete: removing file removes all associated transactions.
    /// Only populated during file processing; may be empty if validation fails.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Constructor for creating a new File aggregate.
    /// 
    /// Validates invariants:
    /// - Id is set and becomes immutable
    /// - FileName is required
    /// - Initial status is always Uploaded
    /// - UploadedAt is set to current UTC time
    /// </summary>
    /// <param name="id">Unique identifier (UUID v7)</param>
    /// <param name="fileName">File name (required, max 255 chars)</param>
    /// <exception cref="ArgumentException">Thrown if fileName is null or empty</exception>
    public File(Guid id, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));

        Id = id;
        FileName = fileName;
        StatusCode = FileStatusCode.Uploaded;
        UploadedAt = DateTime.UtcNow;
        ProcessedAt = null;
        ErrorMessage = null;
    }

    /// <summary>
    /// Constructor for EF Core mapping (parameterless).
    /// Used by the ORM; business logic should use the explicit constructor above.
    /// </summary>
    public File()
    {
    }

    /// <summary>
    /// Transitions file status from Uploaded to Processing.
    /// 
    /// Business rule: Only valid when current status is Uploaded.
    /// Called when the worker starts processing the file from the queue.
    /// 
    /// State machine: Uploaded → Processing
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if status is not Uploaded</exception>
    public void StartProcessing()
    {
        if (StatusCode != FileStatusCode.Uploaded)
            throw new InvalidOperationException(
                $"Cannot transition to Processing from status '{StatusCode}'. " +
                "Only Uploaded files can start processing.");

        StatusCode = FileStatusCode.Processing;
    }

    /// <summary>
    /// Transitions file status to Processed and records completion timestamp.
    /// 
    /// Business rule: Only valid when current status is Processing.
    /// Called after all transactions have been successfully persisted.
    /// 
    /// State machine: Processing → Processed (terminal)
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if status is not Processing</exception>
    public void MarkAsProcessed()
    {
        if (StatusCode != FileStatusCode.Processing)
            throw new InvalidOperationException(
                $"Cannot transition to Processed from status '{StatusCode}'. " +
                "Only files in Processing state can be marked as processed.");

        StatusCode = FileStatusCode.Processed;
        ProcessedAt = DateTime.UtcNow;
        ErrorMessage = null; // Clear any previous error
    }

    /// <summary>
    /// Transitions file status to Rejected and records error details.
    /// 
    /// Business rule: Only valid when current status is Processing.
    /// Called when validation fails or processing encounters an unrecoverable error.
    /// Stores error message for audit trail and user visibility.
    /// 
    /// State machine: Processing → Rejected (terminal)
    /// </summary>
    /// <param name="errorMessage">Description of the error (max 1000 characters)</param>
    /// <exception cref="ArgumentException">Thrown if errorMessage is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown if status is not Processing</exception>
    public void MarkAsRejected(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message is required when rejecting a file.", nameof(errorMessage));

        if (StatusCode != FileStatusCode.Processing)
            throw new InvalidOperationException(
                $"Cannot transition to Rejected from status '{StatusCode}'. " +
                "Only files in Processing state can be rejected.");

        StatusCode = FileStatusCode.Rejected;
        ProcessedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage.Length > 1000 ? errorMessage.Substring(0, 1000) : errorMessage;
    }

    /// <summary>
    /// Adds a transaction to the file's transaction collection.
    /// 
    /// Business rule: Transactions should only be added during processing.
    /// This method is called by the worker as it parses each CNAB line.
    /// 
    /// IMPORTANT: This is a simple collection management method.
    /// Transaction persistence is handled separately by the repository
    /// in a transactional context to ensure all-or-nothing semantics.
    /// </summary>
    /// <param name="transaction">Transaction entity to add to the file</param>
    /// <exception cref="ArgumentNullException">Thrown if transaction is null</exception>
    public void AddTransaction(Transaction transaction)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction), "Transaction cannot be null.");

        transaction.FileId = this.Id;
        Transactions.Add(transaction);
    }

    /// <summary>
    /// Checks if the file is in a terminal state (processing complete).
    /// 
    /// Terminal states are Processed and Rejected.
    /// Files in terminal states cannot transition to other states.
    /// </summary>
    /// <returns>True if file status is Processed or Rejected</returns>
    public bool IsInTerminalState() => FileStatusCode.IsTerminal(StatusCode);

    /// <summary>
    /// Gets the transaction count for this file.
    /// Useful for status queries without loading full collection.
    /// </summary>
    /// <returns>Number of transactions in the file</returns>
    public int GetTransactionCount() => Transactions.Count;
}
