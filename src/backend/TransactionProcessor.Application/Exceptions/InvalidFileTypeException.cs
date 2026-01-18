namespace TransactionProcessor.Application.Exceptions;

/// <summary>
/// Thrown when uploaded file type does not match expected CNAB format.
/// 
/// OWASP A04 (Insecure File Upload) Prevention:
/// - Validates file extension (.txt only for CNAB)
/// - Validates file content markers match CNAB format
/// - Prevents upload of executable or script files
/// 
/// Error Code: INVALID_FILE_TYPE
/// HTTP Status: 400 Bad Request
/// </summary>
public class InvalidFileTypeException : Exception
{
    /// <summary>
    /// File extension that was rejected.
    /// </summary>
    public string? FileExtension { get; set; }

    /// <summary>
    /// Reason why file type is invalid.
    /// </summary>
    public string? Reason { get; set; }

    public InvalidFileTypeException() 
        : base("File type is not supported. Only .txt files are allowed.")
    {
    }

    public InvalidFileTypeException(string message) 
        : base(message)
    {
    }

    public InvalidFileTypeException(string message, Exception? innerException) 
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Create exception with file extension details.
    /// </summary>
    /// <param name="extension">File extension that was rejected</param>
    /// <param name="reason">Reason why extension is invalid</param>
    public InvalidFileTypeException(string extension, string reason)
        : base($"Invalid file type: {extension}. {reason}")
    {
        FileExtension = extension;
        Reason = reason;
    }
}
