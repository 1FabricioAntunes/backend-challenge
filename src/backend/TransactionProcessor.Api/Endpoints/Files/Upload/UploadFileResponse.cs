using System;

namespace TransactionProcessor.Api.Endpoints.Files.Upload;

/// <summary>
/// Response returned after a CNAB file upload is accepted.
/// </summary>
public class UploadFileResponse
{
    /// <summary>
    /// Identifier of the uploaded file.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Sanitized original file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Current processing status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the file was accepted.
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// User-facing message describing the upload result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
