namespace TransactionProcessor.Application.Exceptions;

/// <summary>
/// Exception thrown when file storage operations fail.
/// 
/// Thrown by IFileStorageService (S3) when:
/// - File upload fails due to service error
/// - File download fails (not found or service error)
/// - File delete fails
/// - Network/connectivity issues with S3
/// 
/// This exception is caught by the upload command handler
/// to fail the upload gracefully and mark the file as Rejected.
/// 
/// Retry Behavior:
/// - Transient errors (network, timeout): retried with Polly policy
/// - Non-retryable errors (403 forbidden, 400 bad request): fail immediately
/// 
/// Reference: docs/backend.md § Error Handling
/// </summary>
public class StorageException : Exception
{
    /// <summary>
    /// Error code from the storage service (e.g., AWS S3 error code).
    /// </summary>
    public string? ErrorCode { get; set; }

    public StorageException(string message) : base(message)
    {
    }

    public StorageException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    public StorageException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public StorageException(string message, string errorCode, Exception? innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when message queue operations fail.
/// 
/// Thrown by IMessageQueueService (SQS) when:
/// - Message publish fails due to service error
/// - Message receive fails
/// - Message delete fails
/// - Queue not found or inaccessible
/// 
/// This exception is caught by the upload command handler
/// but doesn't fail the upload (file is already persisted).
/// 
/// Reference: docs/backend.md § Error Handling
/// </summary>
public class QueueException : Exception
{
    /// <summary>
    /// Error code from the queue service (e.g., AWS SQS error code).
    /// </summary>
    public string? ErrorCode { get; set; }

    public QueueException(string message) : base(message)
    {
    }

    public QueueException(string message, Exception? innerException) : base(message, innerException)
    {
    }

    public QueueException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public QueueException(string message, string errorCode, Exception? innerException) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Exception thrown when validation fails.
/// 
/// Thrown by validators when:
/// - File structure validation fails
/// - CNAB format validation fails
/// - Business rule validation fails
/// - Input validation fails
/// 
/// This exception contains detailed validation error information
/// for user-friendly error messages.
/// 
/// Reference: docs/backend.md § Validation § Input Validation
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Collection of validation errors (one per failed validation rule).
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    public ValidationException(string message, IReadOnlyList<string> errors) 
        : base(message)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    public ValidationException(string message, Exception? innerException) 
        : base(message, innerException)
    {
        Errors = new List<string> { message };
    }
}
