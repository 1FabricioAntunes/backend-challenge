using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using TransactionProcessor.Domain.Interfaces;

namespace TransactionProcessor.Infrastructure.Storage;

/// <summary>
/// S3 file storage service implementation.
/// Supports both AWS S3 and LocalStack for development.
/// Includes retry policies for transient failures with exponential backoff.
/// </summary>
public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<S3FileStorageService> _logger;
    private readonly string _bucketName;
    private readonly IAsyncPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the S3FileStorageService.
    /// </summary>
    /// <param name="s3Client">The S3 client for AWS operations.</param>
    /// <param name="configuration">Configuration provider for bucket name and service URL.</param>
    /// <param name="logger">Logger for operation tracking.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required configuration keys are missing.</exception>
    public S3FileStorageService(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3FileStorageService> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _bucketName = _configuration["AWS:S3:BucketName"]
            ?? throw new InvalidOperationException("AWS:S3:BucketName configuration is required");

        _retryPolicy = BuildRetryPolicy();
    }

    /// <summary>
    /// Builds the retry policy for S3 operations with exponential backoff.
    /// </summary>
    /// <returns>An async policy configured for transient S3 failures.</returns>
    private IAsyncPolicy BuildRetryPolicy()
    {
        return Policy
            .Handle<AmazonS3Exception>(IsRetryableException)
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "S3 operation retry attempt {RetryCount} after {DelayMs}ms due to transient failure",
                        retryCount,
                        (int)timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Determines if an S3 exception is retryable based on error code and HTTP status.
    /// Retryable errors: network errors, timeout, 503 service unavailable, 5xx server errors.
    /// Non-retryable errors: 404 not found, 403 forbidden, access denied.
    /// </summary>
    /// <param name="ex">The S3 exception to evaluate.</param>
    /// <returns>True if the exception represents a transient error; otherwise, false.</returns>
    private static bool IsRetryableException(AmazonS3Exception ex)
    {
        // Non-retryable errors: client errors that won't succeed on retry
        if (ex.ErrorCode == "NoSuchKey" || ex.ErrorCode == "NotFound") return false;
        if (ex.ErrorCode == "AccessDenied" || ex.ErrorCode == "Forbidden") return false;
        if (ex.ErrorCode == "InvalidBucketName" || ex.ErrorCode == "InvalidObjectState") return false;

        // Retryable errors: service unavailable, request timeout, internal server errors
        if (ex.ErrorCode == "ServiceUnavailable") return true;
        if (ex.ErrorCode == "RequestTimeout" || ex.ErrorCode == "SlowDown") return true;
        if (ex.ErrorCode == "InternalError") return true;
        if ((int?)ex.StatusCode >= 500) return true;  // Server errors (5xx)
        if (ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout) return true;

        // Network-related errors are retryable
        if (ex.InnerException is HttpRequestException || ex.InnerException is TimeoutException)
            return true;

        return false;
    }

    /// <summary>
    /// Uploads a CNAB file stream to S3 with the specified file ID and name.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <param name="fileStream">The file stream to upload.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="fileId">The unique file identifier for organizing the S3 key.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The S3 object key used to retrieve the file later.</returns>
    /// <remarks>
    /// Key format: cnab/{fileId}/{fileName}
    /// Metadata stored: original-filename, upload-timestamp, file-id, content-type
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// Non-retryable: 404 not found, 403 forbidden, access denied
    /// </remarks>
    public async Task<string> UploadAsync(Stream fileStream, string fileName, Guid fileId, CancellationToken cancellationToken = default)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));
        if (fileId == Guid.Empty)
            throw new ArgumentException("File ID cannot be empty", nameof(fileId));

        try
        {
            var s3Key = $"cnab/{fileId:D}/{fileName}";
            var uploadTimestamp = DateTime.UtcNow.ToString("O");

            _logger.LogInformation("Uploading file to S3: Key={S3Key}, FileId={FileId}, FileName={FileName}", s3Key, fileId, fileName);

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var putObjectRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = s3Key,
                    InputStream = fileStream,
                    ContentType = "application/octet-stream"
                };

                // Add metadata for tracking
                putObjectRequest.Metadata.Add("original-filename", fileName);
                putObjectRequest.Metadata.Add("upload-timestamp", uploadTimestamp);
                putObjectRequest.Metadata.Add("file-id", fileId.ToString());

                var response = await _s3Client.PutObjectAsync(putObjectRequest, cancellationToken);

                _logger.LogInformation("File uploaded successfully to S3: Key={S3Key}, ETag={ETag}", s3Key, response.ETag);
            });

            return s3Key;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error during file upload: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new StorageException($"Failed to upload file to S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during file upload");
            throw new StorageException($"Unexpected error during file upload: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Downloads a file stream from S3 by its object key.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <param name="fileKey">The S3 object key.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The file stream.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the object does not exist in S3.</exception>
    /// <remarks>
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// Non-retryable: 404 not found (FileNotFoundException is thrown instead)
    /// </remarks>
    public async Task<Stream> DownloadAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
            throw new ArgumentException("File key cannot be empty", nameof(fileKey));

        try
        {
            _logger.LogInformation("Downloading file from S3: Key={S3Key}", fileKey);

            var memoryStream = new MemoryStream();

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var getObjectRequest = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileKey
                };

                var response = await _s3Client.GetObjectAsync(getObjectRequest, cancellationToken);

                // Copy to memory stream to allow stream reuse
                await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
            });

            _logger.LogInformation("File downloaded successfully from S3: Key={S3Key}, Size={Size}", fileKey, memoryStream.Length);

            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            _logger.LogWarning("File not found in S3: Key={S3Key}", fileKey);
            throw new FileNotFoundException($"File not found in S3: {fileKey}", fileKey, ex);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error during file download: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new StorageException($"Failed to download file from S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during file download: Key={S3Key}", fileKey);
            throw new StorageException($"Unexpected error during file download: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a file from S3 by its object key.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <param name="fileKey">The S3 object key.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <remarks>
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// </remarks>
    public async Task DeleteAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
            throw new ArgumentException("File key cannot be empty", nameof(fileKey));

        try
        {
            _logger.LogInformation("Deleting file from S3: Key={S3Key}", fileKey);

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = fileKey
                };

                await _s3Client.DeleteObjectAsync(deleteObjectRequest, cancellationToken);
            });

            _logger.LogInformation("File deleted successfully from S3: Key={S3Key}", fileKey);
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error during file deletion: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new StorageException($"Failed to delete file from S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during file deletion: Key={S3Key}", fileKey);
            throw new StorageException($"Unexpected error during file deletion: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks whether a file exists in S3.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <param name="fileKey">The S3 object key.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if the file exists; otherwise, false.</returns>
    /// <remarks>
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// Non-retryable: 404 not found (returns false instead of retrying)
    /// </remarks>
    public async Task<bool> ExistsAsync(string fileKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileKey))
            throw new ArgumentException("File key cannot be empty", nameof(fileKey));

        try
        {
            _logger.LogDebug("Checking file existence in S3: Key={S3Key}", fileKey);

            bool exists = false;

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var getMetadataRequest = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = fileKey
                };

                await _s3Client.GetObjectMetadataAsync(getMetadataRequest, cancellationToken);
                exists = true;
            });

            _logger.LogDebug("File exists in S3: Key={S3Key}", fileKey);
            return exists;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NotFound" || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("File does not exist in S3: Key={S3Key}", fileKey);
            return false;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "AWS S3 error checking file existence: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new StorageException($"Failed to check file existence in S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking file existence: Key={S3Key}", fileKey);
            throw new StorageException($"Unexpected error checking file existence: {ex.Message}", ex);
        }
    }
}
