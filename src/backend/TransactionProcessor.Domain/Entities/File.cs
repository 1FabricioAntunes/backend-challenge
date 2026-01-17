using TransactionProcessor.Domain.ValueObjects;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents an uploaded CNAB file and its processing state.
/// Status transitions: Uploaded → Processing → Processed/Rejected.
/// Uses FileStatusCodes enum for resilient status management.
/// </summary>
public class File
{
    /// <summary>
    /// Unique identifier for the file.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Original uploaded file name (max 255 characters).
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Current processing status (FK to file_statuses lookup table).
    /// Values: Uploaded, Processing, Processed, Rejected (from FileStatusCode enum).
    /// </summary>
    public string StatusCode { get; set; } = FileStatusCode.Uploaded;

    /// <summary>
    /// File size in bytes (BIGINT) for audit and validation.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// S3 storage key (e.g., cnab/{fileId}/{fileName}).
    /// Unique constraint ensures no duplicate storage.
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// User ID who uploaded the file (nullable).
    /// </summary>
    public Guid? UploadedByUserId { get; set; }

    /// <summary>
    /// Timestamp when file was uploaded (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Timestamp when file processing completed (UTC, nullable).
    /// Set after all transactions are persisted or on validation rejection.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Error message if file processing failed (max 1000 characters).
    /// Set when status transitions to Rejected.
    /// </summary>
    public string? ErrorMessage { get; set; }

    // Navigation properties
    /// <summary>
    /// Transactions parsed from this file.
    /// Cascading delete: removing file removes all transactions.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
