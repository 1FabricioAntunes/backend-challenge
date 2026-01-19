using MediatR;

namespace TransactionProcessor.Application.UseCases.Files.Process;

/// <summary>
/// Command to process a CNAB file from S3.
/// Triggers the complete file processing pipeline including parsing, validation, and persistence.
/// </summary>
public class ProcessFileCommand : IRequest<ProcessingResult>
{
    /// <summary>
    /// Identifier of the file to process.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// S3 object key where the file is stored.
    /// Format: cnab/{fileId}/{fileName}
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// Original filename for logging and tracking.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for distributed tracing across logs.
    /// Enables tracking the complete request flow through all services.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Initializes a new ProcessFileCommand.
    /// </summary>
    /// <param name="fileId">The file ID to process</param>
    /// <param name="s3Key">The S3 object key</param>
    /// <param name="fileName">The original filename</param>
    /// <param name="correlationId">Optional correlation ID for tracing</param>
    public ProcessFileCommand(Guid fileId, string s3Key, string fileName, string? correlationId = null)
    {
        FileId = fileId;
        S3Key = s3Key;
        FileName = fileName;
        CorrelationId = correlationId ?? Guid.NewGuid().ToString();
    }
}
