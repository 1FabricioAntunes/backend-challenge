using MediatR;

namespace TransactionProcessor.Application.Commands.Files.Upload;

/// <summary>
/// Command to upload a CNAB file and enqueue it for processing.
/// 
/// This command represents the intent to upload a file from the API endpoint.
/// It contains the file stream, filename, and related metadata needed for S3 upload
/// and SQS message publishing.
/// 
/// Flow:
/// 1. Validate file using IFileValidator
/// 2. Create File aggregate (status = Uploaded)
/// 3. Persist File entity to database
/// 4. Upload file stream to S3
/// 5. Publish processing message to SQS with file metadata
/// 6. Return UploadFileResult with FileId and status
/// 
/// Error Handling:
/// - ValidationException: File structure validation fails
/// - StorageException: S3 upload fails
/// - QueueException: SQS publish fails
/// - All errors result in file marked as Rejected in database
/// 
/// Reference: technical-decisions.md § 6 (Asynchronous Processing Flow)
/// Reference: docs/async-processing.md (Full processing workflow)
/// </summary>
public class UploadFileCommand : IRequest<UploadFileResult>
{
    /// <summary>
    /// File stream containing the CNAB file content.
    /// Must be readable; stream position should be at the beginning (position 0).
    /// Caller is responsible for stream disposal.
    /// </summary>
    public required Stream FileStream { get; init; }

    /// <summary>
    /// Original filename as provided by the client.
    /// Will be sanitized by the handler before storing in database and S3.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// File size in bytes for audit and validation.
    /// Used to enforce size limits before upload.
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Correlation ID for tracking request through the system.
    /// Used in logs and queue message attributes for audit trail.
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// User ID who uploaded the file (optional, for audit trail).
    /// Set by API endpoint from authentication context.
    /// </summary>
    public Guid? UploadedByUserId { get; init; }
}
