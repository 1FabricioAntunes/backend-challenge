namespace TransactionProcessor.Domain.Interfaces;

/// <summary>
/// Abstraction for file storage operations (S3).
/// Allows implementation to switch between AWS S3, Azure Blob, or other providers.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Upload file from stream to storage
    /// </summary>
    /// <param name="fileStream">Stream containing file content</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="fileId">Unique file identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>S3 key/path where file was stored</returns>
    Task<string> UploadAsync(Stream fileStream, string fileName, Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download file from storage as stream
    /// </summary>
    /// <param name="s3Key">S3 key/path of file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing file content</returns>
    Task<Stream> DownloadAsync(string s3Key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if file exists in storage
    /// </summary>
    /// <param name="s3Key">S3 key/path of file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> ExistsAsync(string s3Key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete file from storage
    /// </summary>
    /// <param name="s3Key">S3 key/path of file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string s3Key, CancellationToken cancellationToken = default);
}
