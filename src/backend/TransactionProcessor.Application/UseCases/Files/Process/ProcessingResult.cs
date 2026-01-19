namespace TransactionProcessor.Application.UseCases.Files.Process;

/// <summary>
/// Result of file processing operation.
/// Provides detailed information about success/failure and processing metrics.
/// </summary>
public class ProcessingResult
{
    /// <summary>
    /// Indicates whether file processing succeeded (status = Processed).
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// File ID that was processed.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Current status of the file (Processing, Processed, Rejected).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Number of transactions successfully persisted.
    /// Only populated if Success is true.
    /// </summary>
    public int TransactionsInserted { get; set; }

    /// <summary>
    /// Number of unique stores created or updated.
    /// Only populated if Success is true.
    /// </summary>
    public int StoresUpserted { get; set; }

    /// <summary>
    /// Error message if processing failed (status = Rejected).
    /// Contains details about why the file was rejected.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Collection of validation errors encountered during processing.
    /// Each error includes line number and detailed error message.
    /// Format: "Line {lineNumber}: {errorMessage}"
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Duration of the complete processing operation.
    /// Useful for performance monitoring and logging.
    /// </summary>
    public TimeSpan ProcessingDuration { get; set; }

    /// <summary>
    /// Timestamp when processing started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Timestamp when processing completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }
}
