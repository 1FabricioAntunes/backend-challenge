namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// File status lookup entity representing rows from file_statuses table.
/// 
/// This table stores the definition of each file processing status:
/// - Status code
/// - Description
/// - Is terminal (processing complete)
///
/// Using a lookup table means status logic is stored in database, not hardcoded.
/// </summary>
public class FileStatus
{
    /// <summary>
    /// Status code (e.g., "Uploaded", "Processing", "Processed", "Rejected")
    /// </summary>
    public string StatusCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this status represents a terminal state (processing complete)
    /// </summary>
    public bool IsTerminal { get; set; }
}
