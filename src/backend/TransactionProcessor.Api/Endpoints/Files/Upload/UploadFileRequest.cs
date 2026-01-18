using Microsoft.AspNetCore.Http;

namespace TransactionProcessor.Api.Endpoints.Files.Upload;

/// <summary>
/// Request payload for uploading a CNAB file.
/// </summary>
public class UploadFileRequest
{
    /// <summary>
    /// CNAB file to upload.
    /// </summary>
    public IFormFile File { get; set; } = default!;
}
