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
    public DateTime UploadedAt { get; set; }

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
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public File()
    {
    }

    /// <summary>
    /// Transitions file status from Uploaded to Processing.
    /// 
    /// This method marks the beginning of the file processing workflow.
    /// Called when the worker retrieves the file from the SQS queue and begins
    /// validation and transaction parsing.
    /// 
    /// File Status Lifecycle:
    /// 1. Uploaded (initial): File received and stored in S3, awaiting processing
    /// 2. Processing (this transition): File is being validated and parsed
    /// 3. Processed or Rejected (terminal): Processing completed (success or failure)
    /// 
    /// State Machine Transition: Uploaded → Processing
    /// - Only valid from Uploaded state
    /// - Prerequisite: File must be successfully stored in S3
    /// - Next steps: CNAB parsing, transaction extraction, database persistence
    /// - Exit conditions: Either MarkAsProcessed() or MarkAsRejected()
    /// 
    /// Workflow Context:
    /// 1. User uploads CNAB file (via API endpoint)
    /// 2. File stored in S3 and File entity created with status=Uploaded
    /// 3. File ID published to SQS queue for async processing
    /// 4. Worker receives message, retrieves file from S3
    /// 5. Worker calls StartProcessing() ← This method
    /// 6. Worker validates file structure and parses CNAB lines
    /// 7. Worker calls MarkAsProcessed() or MarkAsRejected() based on result
    /// 8. Client polls status endpoint to retrieve result
    /// 
    /// Reference: docs/async-processing.md § Processing Flow
    /// Reference: docs/business-rules.md § File States
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if current status is not Uploaded</exception>
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
    /// This method marks the successful completion of file processing.
    /// Called after all transactions have been validated and successfully persisted
    /// to the database, and store balances have been updated.
    /// 
    /// File Status Lifecycle:
    /// 1. Uploaded: File received and stored in S3
    /// 2. Processing: File is being validated and parsed
    /// 3. Processed (this transition) [TERMINAL]: All transactions persisted successfully
    /// 4. Alternative: Rejected [TERMINAL]: Validation failed or processing error
    /// 
    /// State Machine Transition: Processing → Processed (TERMINAL)
    /// - Only valid from Processing state
    /// - Prerequisite: All CNAB lines successfully parsed and validated
    /// - Prerequisite: All stores and transactions persisted in database transaction
    /// - Prerequisite: Store balances successfully updated
    /// - Postcondition: File cannot transition to any other state
    /// - Sets ProcessedAt timestamp to current UTC time
    /// - Clears any previous ErrorMessage from failed retry attempts
    /// 
    /// Processing Completion Workflow:
    /// 1. Worker retrieves file from S3
    /// 2. Worker calls StartProcessing()
    /// 3. Worker parses CNAB file (80-character lines)
    /// 4. Worker validates file structure and fields
    /// 5. Worker extracts stores and transactions
    /// 6. Worker starts database transaction (atomic)
    /// 7. Worker upserts stores and inserts transactions
    /// 8. Worker calculates and updates store balances
    /// 9. Worker commits database transaction
    /// 10. Worker calls MarkAsProcessed() ← This method
    /// 11. Worker deletes message from SQS queue
    /// 12. Client receives status=Processed on next poll
    /// 
    /// Data Consistency Requirements:
    /// - Must be called only after successful database transaction commit
    /// - All associated transactions must be persisted before calling this
    /// - Store balance updates must be completed
    /// - If called prematurely, data integrity is compromised
    /// 
    /// Reference: docs/async-processing.md § Success Path
    /// Reference: docs/business-rules.md § File Processing States
    /// Reference: docs/database.md § Transactional Integrity
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if current status is not Processing</exception>
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
    /// Transitions file status to Rejected and records detailed error information.
    /// 
    /// This method marks the failure of file processing.
    /// Called when validation fails or processing encounters an unrecoverable error
    /// that cannot be retried or recovered. The error message is stored for audit
    /// trail and user notification.
    /// 
    /// File Status Lifecycle:
    /// 1. Uploaded: File received and stored in S3
    /// 2. Processing: File is being validated and parsed
    /// 3. Processed [TERMINAL]: All transactions persisted successfully
    /// 4. Rejected (this transition) [TERMINAL]: Validation/processing failed
    /// 
    /// State Machine Transition: Processing → Rejected (TERMINAL)
    /// - Only valid from Processing state
    /// - Prerequisite: Processing attempt was made (or will be abandoned)
    /// - Postcondition: File cannot transition to any other state
    /// - Sets ProcessedAt timestamp to current UTC time
    /// - Stores error message (max 1000 characters) for troubleshooting
    /// - Error message truncated to 1000 chars if longer
    /// 
    /// Rejection Scenarios:
    /// 
    /// 1. VALIDATION FAILURES (before transaction processing):
    ///    - File size exceeds limit (e.g., greater than 10MB)
    ///    - Invalid CNAB format (wrong line length, missing fields)
    ///    - Encoding issues (non-ASCII characters)
    ///    - Missing required header records
    ///    Error: 'CNAB validation failed: Line 5 invalid format (expected 80 chars, got 78)'
    /// 
    /// 2. PARSING ERRORS (during line parsing):
    ///    - Invalid transaction type code (not 1-9)
    ///    - Invalid amount format (non-numeric)
    ///    - Invalid date/time format
    ///    - Invalid CPF/Card format
    ///    Error: 'CNAB parse error on line 10: Invalid transaction type X (expected 1-9)'
    /// 
    /// 3. BUSINESS RULE VIOLATIONS:
    ///    - Amount is zero or negative
    ///    - Store code validation fails
    ///    - Duplicate transaction detection
    ///    Error: 'Business rule violation on line 15: Amount must be greater than 0, got 0'
    /// 
    /// 4. DATABASE ERRORS:
    ///    - Transaction persistence fails (constraint violation, timeout)
    ///    - Connection lost during processing
    ///    - Deadlock detected
    ///    Error: 'Database error: Unique constraint violation on store code ABC123'
    /// 
    /// 5. TRANSACTIONAL ROLLBACK:
    ///    - Processing succeeded for some transactions but failed for others
    ///    - Entire batch rolled back to maintain consistency
    ///    - File marked as Rejected even though partial success occurred
    ///    Error: 'Transaction failed on line 25: Rolled back all changes. Check store balance consistency.'
    /// 
    /// Error Message Usage:
    /// - Stored in database for audit trail
    /// - Returned to client via status endpoint
    /// - Displayed in admin/monitoring dashboards
    /// - Logged with structured logging and correlation IDs
    /// - Used for alerts and operational troubleshooting
    /// 
    /// Retry Strategy:
    /// - File remains in S3 and can be reprocessed
    /// - Admin can manually retry (future enhancement)
    /// - No automatic retries for rejected files
    /// - New upload required to reprocess if file was corrupted
    /// 
    /// DLQ Behavior:
    /// - If processing error (type 4-5), message moves to DLQ after max retries
    /// - If validation error (type 1-3), message deleted (no retry)
    /// - DLQ for operational monitoring and investigation
    /// 
    /// Reference: docs/async-processing.md § Failure Path and Error Handling
    /// Reference: docs/business-rules.md § File Rejection Rules
    /// Reference: technical-decisions.md § Error Classification
    /// </summary>
    /// <param name="errorMessage">Description of why file processing failed (max 1000 characters).
    /// Should be specific and actionable for troubleshooting.
    /// Example: 'CNAB validation failed: Line 10 has invalid type code X (expected 1-9)'
    /// </param>
    /// <exception cref="ArgumentException">Thrown if errorMessage is null or empty</exception>
    /// <exception cref="InvalidOperationException">Thrown if current status is not Processing</exception>
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
