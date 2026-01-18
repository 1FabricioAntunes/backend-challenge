namespace TransactionProcessor.Application.Exceptions;

/// <summary>
/// Thrown when uploaded file size exceeds maximum allowed limit.
/// 
/// OWASP A04 (Insecure File Upload) Prevention:
/// - Enforces maximum file size (10MB for CNAB processing)
/// - Prevents Denial of Service (DoS) attacks via large uploads
/// - Protects against buffer overflow and memory exhaustion
/// 
/// Error Code: FILE_TOO_LARGE
/// HTTP Status: 400 Bad Request
/// 
/// Limit Rationale:
/// - CNAB files are typically 10-100KB for normal transaction batches
/// - 10MB limit accommodates large batches (500K+ transactions)
/// - Protects memory and storage resources
/// - Aligns with industry standards for batch file uploads
/// </summary>
public class FileTooLargeException : Exception
{
    /// <summary>
    /// Actual file size in bytes.
    /// </summary>
    public long ActualSize { get; set; }

    /// <summary>
    /// Maximum allowed file size in bytes.
    /// </summary>
    public long MaxSize { get; set; }

    public FileTooLargeException() 
        : base("File size exceeds maximum allowed limit (10MB).")
    {
    }

    public FileTooLargeException(string message) 
        : base(message)
    {
    }

    public FileTooLargeException(string message, Exception? innerException) 
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Create exception with file size details.
    /// </summary>
    /// <param name="actualSize">Actual file size in bytes</param>
    /// <param name="maxSize">Maximum allowed file size in bytes</param>
    public FileTooLargeException(long actualSize, long maxSize)
        : base($"File size {FormatBytes(actualSize)} exceeds maximum {FormatBytes(maxSize)}.")
    {
        ActualSize = actualSize;
        MaxSize = maxSize;
    }

    /// <summary>
    /// Format bytes as human-readable size (B, KB, MB, GB).
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024M:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024M * 1024):F1} MB";
        return $"{bytes / (1024M * 1024 * 1024):F1} GB";
    }
}
