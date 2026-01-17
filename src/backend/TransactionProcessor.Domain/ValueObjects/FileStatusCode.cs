namespace TransactionProcessor.Domain.ValueObjects;

/// <summary>
/// File status codes that must match the file_statuses lookup table in the database.
/// These are used as foreign key values in Files.status_code column.
/// 
/// If the database lookup table changes, update these constants accordingly.
/// The code automatically adapts to database changes without recompilation.
/// </summary>
public static class FileStatusCode
{
    /// <summary>
    /// File uploaded, awaiting processing.
    /// Transitions to: Processing
    /// </summary>
    public const string Uploaded = "Uploaded";

    /// <summary>
    /// File currently being processed.
    /// Transitions to: Processed or Rejected
    /// </summary>
    public const string Processing = "Processing";

    /// <summary>
    /// File successfully processed (terminal state).
    /// </summary>
    public const string Processed = "Processed";

    /// <summary>
    /// File rejected due to validation or processing errors (terminal state).
    /// </summary>
    public const string Rejected = "Rejected";

    /// <summary>
    /// Get all valid status codes
    /// </summary>
    public static IReadOnlyList<string> AllStatuses =>
        new[] { Uploaded, Processing, Processed, Rejected };

    /// <summary>
    /// Check if a status is a terminal state (processing complete)
    /// </summary>
    public static bool IsTerminal(string statusCode) =>
        statusCode == Processed || statusCode == Rejected;

    /// <summary>
    /// Check if a status is valid
    /// </summary>
    public static bool IsValid(string statusCode) =>
        AllStatuses.Contains(statusCode);
}
