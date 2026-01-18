namespace TransactionProcessor.Application.Exceptions;

/// <summary>
/// Thrown when filename is invalid or cannot be sanitized to a safe value.
/// 
/// OWASP A03 (Injection) and A04 (Insecure File Upload) Prevention:
/// - Rejects filenames with path traversal characters (../, ..\\, :)
/// - Rejects filenames with shell metacharacters (;, |, &, >, <)
/// - Rejects filenames with SQL injection attempts (--, /*, */)
/// - Enforces maximum filename length (255 chars)
/// - Requires valid filename after sanitization
/// 
/// Error Code: INVALID_FILE_NAME
/// HTTP Status: 400 Bad Request
/// 
/// Examples of Invalid Filenames:
/// - "../../etc/passwd.txt" (path traversal)
/// - "file; rm -rf /" (shell injection)
/// - "file'; DROP TABLE users; --" (SQL injection)
/// - "x" * 1000 (too long)
/// - "   " (whitespace only, becomes empty after sanitization)
/// 
/// Security Rationale:
/// - Filenames are stored in database and displayed to users
/// - Injection in filename could escape to logs or reports
/// - Path traversal could write files to unexpected locations
/// - Ensures all stored filenames are safe for display and storage
/// </summary>
public class InvalidFileNameException : Exception
{
    /// <summary>
    /// Original filename that was rejected.
    /// </summary>
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// Reason why filename is invalid.
    /// </summary>
    public string? Reason { get; set; }

    public InvalidFileNameException() 
        : base("Filename is invalid or contains forbidden characters.")
    {
    }

    public InvalidFileNameException(string message) 
        : base(message)
    {
    }

    public InvalidFileNameException(string message, Exception? innerException) 
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Create exception with filename details.
    /// </summary>
    /// <param name="originalFileName">Original filename that was rejected</param>
    /// <param name="reason">Reason why filename is invalid</param>
    public InvalidFileNameException(string originalFileName, string reason)
        : base($"Invalid filename: '{TruncateForLog(originalFileName)}'. {reason}")
    {
        OriginalFileName = originalFileName;
        Reason = reason;
    }

    /// <summary>
    /// Truncate filename for safe logging (prevent log injection).
    /// </summary>
    private static string TruncateForLog(string fileName, int maxLength = 100)
    {
        if (fileName.Length <= maxLength)
            return fileName;
        
        return fileName[..(maxLength - 3)] + "...";
    }
}
