namespace TransactionProcessor.Application.DTOs;

/// <summary>
/// Data Transfer Object for File entity with status and processing information
/// </summary>
public class FileDto
{
    /// <summary>
    /// Unique identifier for the file
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Original filename of the uploaded CNAB file
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Current processing status of the file
    /// Possible values: Uploaded, Processing, Processed, Rejected
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the file was uploaded (ISO 8601 UTC format)
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Timestamp when the file processing completed (ISO 8601 UTC format)
    /// Null if file is still pending or processing
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Total number of transactions parsed from the file
    /// Null if file has not been processed yet
    /// </summary>
    public int? TransactionCount { get; set; }

    /// <summary>
    /// Error message if file processing failed or was rejected
    /// Null if no errors occurred
    /// </summary>
    public string? ErrorMessage { get; set; }
}
