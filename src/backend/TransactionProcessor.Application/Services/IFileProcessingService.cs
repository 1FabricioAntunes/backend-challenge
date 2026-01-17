using TransactionProcessor.Application.Models;

namespace TransactionProcessor.Application.Services;

/// <summary>
/// Result of file processing operation.
/// </summary>
public class FileProcessingResult
{
    /// <summary>
    /// Indicates if processing succeeded (file moved to Processed status).
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if processing failed or validation rejected the file.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of stores upserted during processing.
    /// </summary>
    public int StoresUpserted { get; set; }

    /// <summary>
    /// Number of transactions inserted during processing.
    /// </summary>
    public int TransactionsInserted { get; set; }

    /// <summary>
    /// Validation errors if file was rejected due to validation failures.
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();
}

/// <summary>
/// Service for processing CNAB files retrieved from S3.
/// Orchestrates parsing, validation, and persistence with transactional guarantees.
/// </summary>
public interface IFileProcessingService
{
    /// <summary>
    /// Processes a CNAB file for a given file ID.
    /// 
    /// Flow:
    /// 1. Update file status to Processing
    /// 2. Retrieve file content from S3
    /// 3. Parse and validate CNAB format
    /// 4. Check idempotency (skip if already processed)
    /// 5. In transaction: upsert stores, insert transactions, update balance
    /// 6. Update file status to Processed
    /// 7. On any error: mark file as Rejected with error details
    /// </summary>
    /// <param name="fileId">Unique identifier of file to process</param>
    /// <param name="s3Key">S3 object key where file is stored</param>
    /// <param name="fileName">Original file name (for logging)</param>
    /// <param name="correlationId">Correlation ID for logging</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result with success status and details</returns>
    Task<FileProcessingResult> ProcessFileAsync(
        Guid fileId,
        string s3Key,
        string fileName,
        string correlationId,
        CancellationToken cancellationToken = default);
}
