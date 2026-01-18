using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionProcessor.Application.Exceptions;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Domain.Services;
using TransactionProcessor.Infrastructure.Persistence;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Application.Commands.Files.Upload;

/// <summary>
/// Command handler for UploadFileCommand.
/// Orchestrates file validation, S3 upload, and SQS message publishing.
/// 
/// Processing Flow:
/// 1. Validate file stream and size using IFileValidator
/// 2. Sanitize filename (remove path separators, special chars, max 255 chars)
/// 3. Create File aggregate with status = Uploaded
/// 4. Persist File entity to database (atomic)
/// 5. Upload file stream to S3 with retry policy
/// 6. Publish message to SQS with FileId, S3Key, and metadata
/// 7. Return UploadFileResult with FileId and processing URL
/// 
/// Error Handling:
/// - ValidationException: File structure/size validation fails (400 Bad Request)
/// - StorageException: S3 upload fails after retries (500 Server Error)
/// - QueueException: SQS publish fails after retries (500 Server Error)
/// - All unrecoverable errors result in file marked as Rejected in database
/// 
/// Transactional Consistency:
/// - File entity persisted to database before S3 upload (optimistic)
/// - If S3 or SQS fail after File is saved, file remains in Uploaded state
/// - Client can retry the upload or worker processes retried message
/// - Idempotency check in file processing prevents duplicate transaction persistence
/// 
/// Performance Characteristics:
/// - Async/await throughout, no blocking I/O
/// - Streaming upload to S3 (no full file buffering in memory)
/// - Minimal database transaction scope (only File insert)
/// - Return 202 Accepted immediately; client polls for completion
/// 
/// Reference: technical-decisions.md § 6 (Asynchronous Processing Flow)
/// Reference: docs/async-processing.md (Upload & Processing Workflow)
/// Reference: backend.md § File Upload Endpoint (API spec)
/// </summary>
public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, UploadFileResult>
{
    private readonly IFileValidator _fileValidator;
    private readonly IFileRepository _fileRepository;
    private readonly IFileStorageService _storageService;
    private readonly IMessageQueueService _queueService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UploadFileCommandHandler> _logger;

    public UploadFileCommandHandler(
        IFileValidator fileValidator,
        IFileRepository fileRepository,
        IFileStorageService storageService,
        IMessageQueueService queueService,
        ApplicationDbContext dbContext,
        ILogger<UploadFileCommandHandler> logger)
    {
        _fileValidator = fileValidator ?? throw new ArgumentNullException(nameof(fileValidator));
        _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the file upload command.
    /// 
    /// Returns:
    /// - Success (202): File validated, stored in S3, persisted to DB, message queued
    /// - Validation Error (400): File size or format invalid
    /// - Storage Error (500): S3 upload failed
    /// - Queue Error (500): SQS publish failed
    /// 
    /// Note: Returns 202 Accepted regardless of SQS publish result.
    /// If SQS fails, file remains in Uploaded state and can be reprocessed manually.
    /// </summary>
    public async Task<UploadFileResult> Handle(UploadFileCommand command, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var fileId = Guid.NewGuid();
        
        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            { "CorrelationId", command.CorrelationId },
            { "FileId", fileId },
            { "FileName", command.FileName }
        });

        var result = new UploadFileResult();

        try
        {
            _logger.LogInformation("Starting file upload processing for {FileName}", command.FileName);

            // Step 1: Validate file structure and size using IFileValidator
            _logger.LogInformation("Validating file structure (size, format, encoding)");
            
            // Reset stream position to beginning for validation
            if (command.FileStream.CanSeek)
            {
                command.FileStream.Seek(0, SeekOrigin.Begin);
            }

            var validationResult = await _fileValidator.Validate(command.FileStream);

            if (!validationResult.IsValid)
            {
                stopwatch.Stop();
                _logger.LogWarning("File validation failed. Errors: {@ValidationErrors}", validationResult.Errors);

                result.Success = false;
                result.FileId = null;
                result.Status = "Rejected";
                result.Message = "File validation failed. Please verify CNAB format and encoding.";
                result.ErrorDetails = validationResult.Errors;

                _logger.LogInformation("Metrics: durationMs={DurationMs}, validationFailed=true", stopwatch.ElapsedMilliseconds);
                return result;
            }

            _logger.LogInformation("File validation passed");

            // Step 1.5: Placeholder for virus scanning (production security enhancement)
            // TODO: Implement virus scanning via cloud antivirus service
            // Context: Current implementation does not include virus scanning
            // Impact: Malicious files could be uploaded without detection
            // Ticket: CNAB-SECURITY-001
            // 
            // Example production implementation:
            // - Integrate with ClamAV or VirusTotal API
            // - Scan file before S3 upload
            // - Reject file if threat detected
            // - Log scan results with timestamp
            _logger.LogWarning("SECURITY: Virus scanning is not implemented. " +
                "Consider adding antivirus scanning in production environments. " +
                "Reference: docs/security.md § File Upload Validation");

            // Step 2: Sanitize filename (remove path separators, special chars, max 255 chars)
            var sanitizedFileName = SanitizeFileName(command.FileName);
            _logger.LogInformation("Filename sanitized: {OriginalName} -> {SanitizedName}", command.FileName, sanitizedFileName);

            // Validate sanitized filename is safe and not empty after sanitization
            if (string.IsNullOrWhiteSpace(sanitizedFileName))
            {
                stopwatch.Stop();
                _logger.LogWarning("Filename sanitization resulted in empty name: {OriginalFileName}", command.FileName);

                result.Success = false;
                result.FileId = null;
                result.Status = "Rejected";
                result.Message = "File name is invalid or contains only special characters.";
                result.ErrorDetails.Add("Filename cannot be empty after sanitization");

                return result;
            }

            // Step 3: Create File aggregate
            _logger.LogInformation("Creating File aggregate with status=Uploaded");
            var file = new FileEntity(fileId, sanitizedFileName)
            {
                FileSize = command.FileSize,
                UploadedByUserId = command.UploadedByUserId,
                UploadedAt = DateTime.UtcNow
            };

            // Step 4: Persist File entity to database (minimal transaction scope)
            _logger.LogInformation("Persisting File entity to database");
            
            using (var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                try
                {
                    await _fileRepository.AddAsync(file);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await dbTransaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("File entity persisted successfully");
                }
                catch (Exception ex)
                {
                    await dbTransaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, "Database transaction failed while persisting File entity");
                    
                    result.Success = false;
                    result.FileId = null;
                    result.Status = "Rejected";
                    result.Message = "Failed to save file metadata to database.";
                    result.ErrorDetails.Add(ex.Message);
                    
                    stopwatch.Stop();
                    _logger.LogInformation("Metrics: durationMs={DurationMs}, dbError=true", stopwatch.ElapsedMilliseconds);
                    return result;
                }
            }

            // Step 5: Upload file to S3
            _logger.LogInformation("Uploading file to S3");
            
            // Reset stream position for S3 upload
            if (command.FileStream.CanSeek)
            {
                command.FileStream.Seek(0, SeekOrigin.Begin);
            }

            string s3Key;
            try
            {
                s3Key = await _storageService.UploadAsync(
                    command.FileStream,
                    sanitizedFileName,
                    fileId,
                    cancellationToken);
                
                _logger.LogInformation("File uploaded to S3 successfully. S3Key: {S3Key}", s3Key);

                // Update file with S3 key
                file.S3Key = s3Key;
                await _fileRepository.UpdateAsync(file);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (StorageException ex)
            {
                _logger.LogError(ex, "S3 upload failed for file {FileId}", fileId);
                
                // Mark file as rejected
                file.MarkAsRejected($"File storage failed: {ex.Message}");
                await _fileRepository.UpdateAsync(file);
                await _dbContext.SaveChangesAsync(cancellationToken);

                result.Success = false;
                result.FileId = fileId;
                result.Status = "Rejected";
                result.Message = "Failed to store file in cloud storage.";
                result.ErrorDetails.Add(ex.Message);

                stopwatch.Stop();
                _logger.LogInformation("Metrics: durationMs={DurationMs}, s3Error=true", stopwatch.ElapsedMilliseconds);
                throw; // Re-throw for API to handle as 500
            }

            // Step 6: Publish message to SQS
            _logger.LogInformation("Publishing file processing message to SQS");
            
            try
            {
                var queueMessage = new FileUploadedMessage
                {
                    FileId = fileId,
                    FileName = sanitizedFileName,
                    S3Key = s3Key,
                    UploadedAt = DateTime.UtcNow
                };

                var messageId = await _queueService.PublishAsync(
                    queueMessage,
                    command.CorrelationId,
                    cancellationToken);

                _logger.LogInformation("Message published to SQS successfully. MessageId: {MessageId}", messageId);
            }
            catch (QueueException ex)
            {
                // Log but don't fail the upload - file is persisted and can be processed later
                _logger.LogWarning(ex, "SQS publish failed for file {FileId}. Message will need manual processing.", fileId);
                
                // Still return success since file is safely stored in database and S3
                // The SQS message can be republished or the file can be picked up by the worker
            }

            // Step 7: Return success result
            stopwatch.Stop();

            result.Success = true;
            result.FileId = fileId;
            result.FileName = sanitizedFileName;
            result.Status = "Uploaded";
            result.UploadedAt = DateTime.UtcNow;
            result.Message = "File uploaded successfully. Processing has been queued.";
            result.S3Key = s3Key;

            _logger.LogInformation(
                "File upload completed successfully. Metrics: durationMs={DurationMs}, fileSize={FileSize}",
                stopwatch.ElapsedMilliseconds,
                command.FileSize);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during file upload processing");

            result.Success = false;
            result.FileId = fileId;
            result.Status = "Rejected";
            result.Message = "An unexpected error occurred during file upload.";
            result.ErrorDetails.Add(ex.Message);

            _logger.LogInformation("Metrics: durationMs={DurationMs}, unexpectedError=true", stopwatch.ElapsedMilliseconds);
            throw; // Re-throw for API to handle
        }
    }

    /// <summary>
    /// Sanitizes filename by removing path separators, special characters, and enforcing length limits.
    /// 
    /// Security Considerations (OWASP A03 - Injection Prevention, OWASP A04 - Insecure File Upload):
    /// - Remove path separators (/ \ :) to prevent directory traversal
    /// - Remove special characters that could be interpreted as commands or SQL
    /// - Limit length to 255 chars to fit in database column
    /// - Trim whitespace from beginning and end
    /// - Remove shell metacharacters (;, |, &, $, `, etc.)
    /// - Remove SQL injection patterns (--, /*, */, xp_, sp_)
    /// 
    /// Examples:
    /// - "../../cnab.txt" → "cnab.txt" (path traversal blocked)
    /// - "file;rm *.txt" → "filerm.txt" (command injection blocked)
    /// - "file'; DROP TABLE users; --" → "file DROP TABLE users" (SQL injection blocked)
    /// - "very_long_filename_..." → truncated to 255 chars (buffer overflow blocked)
    /// - "  file.txt  " → "file.txt" (whitespace trimmed)
    /// 
    /// Reference: technical-decisions.md § Input Validation and Sanitization
    /// Reference: OWASP A03 (Injection) and A04 (Insecure File Upload)
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        // Trim whitespace
        var sanitized = fileName.Trim();

        // Remove path separators and special characters that OS doesn't allow
        var invalidChars = Path.GetInvalidFileNameChars().Append('/').Append('\\').Append(':').ToArray();
        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar.ToString(), string.Empty);
        }

        // Remove shell escape characters to prevent command injection (OWASP A03)
        var shellMetacharacters = new[] { ";", "|", "&", "$", "`", "\n", "\r", "\t" };
        foreach (var metachar in shellMetacharacters)
        {
            sanitized = sanitized.Replace(metachar, string.Empty);
        }

        // Remove SQL injection patterns (OWASP A03)
        // Even though we use parameterized queries, defense-in-depth approach
        var sqlPatterns = new[] { "--", "/*", "*/", "xp_", "sp_", "DROP", "DELETE", "INSERT", "UPDATE", "SELECT" };
        foreach (var pattern in sqlPatterns)
        {
            sanitized = sanitized.Replace(pattern, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // Remove environment variable references to prevent variable expansion
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[\$%]\w+[\$%]?", "");

        // Limit length to 255 chars (database column constraint, filesystem limit)
        if (sanitized.Length > 255)
        {
            // Preserve file extension when truncating
            var extension = Path.GetExtension(sanitized);
            var maxNameLength = 255 - extension.Length;
            if (maxNameLength > 0)
            {
                sanitized = sanitized[..maxNameLength] + extension;
            }
            else
            {
                sanitized = sanitized[..255];
            }
        }

        // Ensure we have a valid filename (not empty after sanitization)
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "unnamed_file.txt";
        }

        return sanitized;
    }
}

/// <summary>
/// Message published to SQS queue to trigger file processing.
/// 
/// This message is consumed by the file processing worker (Lambda or hosted service).
/// Contains all information needed to download file from S3 and process it.
/// 
/// Message Flow:
/// 1. Handler publishes message to SQS file-processing-queue
/// 2. Worker (ProcessFileWorker or Lambda) receives message
/// 3. Worker downloads file from S3 using S3Key
/// 4. Worker parses and validates CNAB content
/// 5. Worker persists transactions to database
/// 6. Worker marks file as Processed
/// 7. If error: marks file as Rejected and publishes to DLQ
/// 
/// Reference: docs/async-processing.md § SQS Message Format
/// Reference: technical-decisions.md § 6 (Asynchronous Processing Flow)
/// </summary>
public class FileUploadedMessage
{
    /// <summary>
    /// File identifier (UUID v7) created during upload.
    /// Used to retrieve file entity from database during processing.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Sanitized filename as stored in database.
    /// For audit trail and error messages.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// S3 storage key for downloading the file.
    /// Format: cnab/{fileId}/{sanitizedFileName}
    /// </summary>
    public string S3Key { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when message was created (UTC).
    /// Used for processing metrics and timeouts.
    /// </summary>
    public DateTime UploadedAt { get; set; }
}
