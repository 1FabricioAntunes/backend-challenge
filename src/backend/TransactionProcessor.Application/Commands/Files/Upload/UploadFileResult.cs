using System.Text.Json.Serialization;

namespace TransactionProcessor.Application.Commands.Files.Upload;

/// <summary>
/// Result returned after file upload command completes.
/// Contains success/failure status, file ID, and processing details.
/// 
/// Success Case (HTTP 202 Accepted):
/// - Success = true
/// - FileId = UUID of created File entity
/// - Status = "Uploaded" (file accepted, processing queued)
/// - UploadedAt = timestamp when file was persisted
/// - Message = "File uploaded successfully. Processing has been queued."
/// - S3Key = S3 path to uploaded file
/// - ErrorDetails = null
/// 
/// Failure Case (HTTP 400/500):
/// - Success = false
/// - FileId = null or partial ID
/// - Status = "Rejected"
/// - Message = error description
/// - ErrorDetails = detailed validation/processing errors
/// 
/// Reference: technical-decisions.md § API Design (Error Handling)
/// Reference: docs/async-processing.md § Upload Response Format
/// </summary>
public class UploadFileResult
{
    /// <summary>
    /// Flag indicating success or failure.
    /// True if file was successfully stored and processing queued.
    /// False if validation or storage failed.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Unique identifier of the created File entity.
    /// Used by client to poll for processing status via GET /api/files/v1/{fileId}.
    /// Null if file creation failed.
    /// </summary>
    [JsonPropertyName("fileId")]
    public Guid? FileId { get; set; }

    /// <summary>
    /// Sanitized filename as stored in database.
    /// May differ from original filename (special chars removed, truncated to 255 chars).
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Current file processing status.
    /// Success: "Uploaded" (processing queued, not yet started)
    /// Failure: "Rejected" (validation or processing error)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when file was created (UTC, ISO 8601 format).
    /// Example: "2026-01-13T14:30:00.000Z"
    /// </summary>
    [JsonPropertyName("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// User-friendly message describing upload result.
    /// Success: "File uploaded successfully. Processing has been queued."
    /// Failure: Validation error description
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// S3 storage key for uploaded file (if successful).
    /// Format: cnab/{fileId}/{sanitizedFileName}
    /// Used internally for file retrieval during processing.
    /// Null if upload failed.
    /// </summary>
    [JsonPropertyName("s3Key")]
    public string? S3Key { get; set; }

    /// <summary>
    /// Detailed error information for validation/processing failures.
    /// Contains list of validation errors if file validation failed.
    /// Null if upload succeeded.
    /// </summary>
    [JsonPropertyName("errorDetails")]
    public IList<string> ErrorDetails { get; set; } = new List<string>();
}
