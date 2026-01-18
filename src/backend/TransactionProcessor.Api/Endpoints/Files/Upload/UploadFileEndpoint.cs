using System.Diagnostics;
using FastEndpoints;
using MediatR;
using TransactionProcessor.Application.Commands.Files.Upload;
using TransactionProcessor.Application.Exceptions;

namespace TransactionProcessor.Api.Endpoints.Files.Upload;

/// <summary>
/// FastEndpoints endpoint for uploading CNAB files.
/// 
/// Route: POST /api/files/v1
/// Content-Type: multipart/form-data
/// 
/// Request:
/// - File: IFormFile containing CNAB data (required)
/// 
/// Response (202 Accepted):
/// - FileId: Unique identifier for tracking
/// - FileName: Sanitized filename
/// - Status: "Uploaded" (processing queued)
/// - UploadedAt: Timestamp (ISO 8601 UTC)
/// - Message: User-friendly message
/// 
/// Error Responses:
/// - 400 Bad Request: Validation failed (file missing, wrong type, invalid content)
/// - 413 Payload Too Large: File exceeds 10MB limit
/// - 500 Internal Server Error: Server error (S3, SQS, database)
/// 
/// Flow:
/// 1. Validate request (file present, size check)
/// 2. Generate correlation ID for tracking through system
/// 3. Create UploadFileCommand with file stream
/// 4. Send via MediatR to UploadFileCommandHandler
/// 5. Handler validates, stores in S3, publishes to SQS
/// 6. Return 202 Accepted with FileId for polling
/// 
/// Non-Blocking:
/// - Returns immediately (202 Accepted)
/// - Actual processing happens asynchronously in worker
/// - Client polls GET /api/files/v1/{fileId} for status
/// 
/// Reference: docs/backend.md § File Upload Endpoint
/// Reference: technical-decisions.md § API Design § Error Handling
/// </summary>
public class UploadFileEndpoint : Endpoint<UploadFileRequest, UploadFileResponse>
{
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private readonly IMediator _mediator;

    public UploadFileEndpoint(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Post("/api/files/v1");
        AllowAnonymous();
        AllowFileUploads(); // Enable multipart/form-data
        
        Description(d => d
            .Produces<UploadFileResponse>(202, "application/json")
            .Produces<ErrorResponse>(400, "application/json")
            .Produces<ErrorResponse>(413, "application/json")
            .Produces<ErrorResponse>(500, "application/json")
            .WithTags("Files")
            .WithSummary("Upload CNAB file")
            .WithDescription("Upload a CNAB file for processing. Returns 202 Accepted with file ID for polling status."));
    }

    public override async Task HandleAsync(UploadFileRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = HttpContext.Request.Headers["X-Correlation-ID"].ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        try
        {
            // Validate file is present
            if (req.File == null || req.File.Length == 0)
            {
                HttpContext.Response.StatusCode = 400;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(
                        "File is required and cannot be empty.",
                        "MISSING_FILE",
                        400),
                    cancellationToken: ct);
                return;
            }

            // Validate file size (10MB limit)
            if (req.File.Length > MaxFileSize)
            {
                HttpContext.Response.StatusCode = 413;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(
                        $"File size {req.File.Length} bytes exceeds maximum {MaxFileSize} bytes (10MB).",
                        "FILE_TOO_LARGE",
                        413),
                    cancellationToken: ct);
                return;
            }

            // Get file stream
            using var fileStream = req.File.OpenReadStream();

            // Create command for handler
            var command = new UploadFileCommand
            {
                FileStream = fileStream,
                FileName = req.File.FileName,
                FileSize = req.File.Length,
                CorrelationId = correlationId,
                UploadedByUserId = null // Could be populated from auth context if available
            };

            // Send command via MediatR to handler
            var result = await _mediator.Send(command, ct);

            // Check if handler encountered validation error
            if (!result.Success && result.ErrorDetails.Any())
            {
                HttpContext.Response.StatusCode = 400;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(
                        result.Message ?? "File validation failed.",
                        "VALIDATION_ERROR",
                        400,
                        result.ErrorDetails),
                    cancellationToken: ct);
                return;
            }

            // Check if handler encountered storage error
            if (!result.Success && result.Status == "Rejected")
            {
                HttpContext.Response.StatusCode = 500;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(
                        result.Message ?? "An error occurred during file processing.",
                        "PROCESSING_ERROR",
                        500),
                    cancellationToken: ct);
                return;
            }

            // Success - return 202 Accepted
            stopwatch.Stop();
            HttpContext.Response.StatusCode = 202;
            HttpContext.Response.Headers["X-Correlation-ID"] = correlationId;
            HttpContext.Response.Headers["X-Processing-Time-Ms"] = stopwatch.ElapsedMilliseconds.ToString();

            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            // Log exception (structured logging should be configured)
            Console.Error.WriteLine($"[{correlationId}] Error in UploadFileEndpoint: {ex}");

            HttpContext.Response.StatusCode = 500;
            await HttpContext.Response.WriteAsJsonAsync(
                new ErrorResponse(
                    "An unexpected error occurred during file upload.",
                    "INTERNAL_ERROR",
                    500),
                cancellationToken: ct);
        }
    }
}

/// <summary>
/// Standard error response format for all API errors.
/// Provides consistent error information across all endpoints.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// User-friendly error message.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Machine-readable error code for client-side handling.
    /// Examples: VALIDATION_ERROR, FILE_TOO_LARGE, INTERNAL_ERROR
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// HTTP status code returned.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Detailed error information (validation errors, etc.).
    /// </summary>
    public IList<string>? Details { get; set; }

    public ErrorResponse(string message, string code, int statusCode, IList<string>? details = null)
    {
        Message = message;
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }
}
