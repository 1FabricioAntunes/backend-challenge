using System;
using System.IO;
using System.Threading.Tasks;

namespace TransactionProcessor.Application.Interfaces;

/// <summary>
/// Abstraction for file storage operations used by the application.
/// Enables swapping storage providers (e.g., AWS S3, Azure Blob) via dependency injection.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Uploads a CNAB file stream to the configured storage provider.
    /// </summary>
    /// <param name="fileStream">The file content stream to upload.</param>
    /// <param name="fileName">The original file name for metadata and organization.</param>
    /// <param name="fileId">The unique identifier of the file entity used to build the storage key.</param>
    /// <returns>
    /// A task that resolves to the storage object key used to retrieve the file later.
    /// Recommended key pattern: <c>cnab/{fileId}/{fileName}</c>.
    /// </returns>
    Task<string> UploadAsync(Stream fileStream, string fileName, Guid fileId);

    /// <summary>
    /// Downloads a file stream from the storage provider by its object key.
    /// </summary>
    /// <param name="fileKey">The storage object key returned from <see cref="UploadAsync(Stream, string, Guid)"/>.</param>
    /// <returns>The file content stream.</returns>
    /// <remarks>
    /// Implementations should throw a <see cref="FileNotFoundException"/> if the object does not exist
    /// and distinguish service errors from not-found cases.
    /// </remarks>
    Task<Stream> DownloadAsync(string fileKey);

    /// <summary>
    /// Deletes a file from the storage provider.
    /// </summary>
    /// <param name="fileKey">The storage object key to delete.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    Task DeleteAsync(string fileKey);

    /// <summary>
    /// Checks whether a file exists in the storage provider.
    /// </summary>
    /// <param name="fileKey">The storage object key to check.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    Task<bool> ExistsAsync(string fileKey);
}
