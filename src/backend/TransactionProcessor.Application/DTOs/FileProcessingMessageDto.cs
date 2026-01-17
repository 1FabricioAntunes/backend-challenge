namespace TransactionProcessor.Application.DTOs;

/// <summary>
/// Data transfer object for file processing messages from SQS.
/// Represents the information needed to process a CNAB file.
/// </summary>
public class FileProcessingMessageDto
{
    /// <summary>
    /// Unique identifier of the file to process.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// S3 object key where the CNAB file is stored.
    /// Format: cnab/{fileId}/{filename}
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// Original file name as uploaded by user.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when file was uploaded.
    /// ISO 8601 format.
    /// </summary>
    public DateTime UploadedAt { get; set; }
}
