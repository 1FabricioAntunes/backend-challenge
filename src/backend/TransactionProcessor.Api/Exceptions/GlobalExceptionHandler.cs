using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using TransactionProcessor.Api.Models;
using FluentValidation;

namespace TransactionProcessor.Api.Exceptions;

/// <summary>
/// Global exception handler for FastEndpoints
/// Handles all unhandled exceptions and maps them to appropriate HTTP responses
/// 
/// Usage: Register in Program.cs with app.UseExceptionHandler()
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    /// <summary>
    /// Constructor
    /// </summary>
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the exception and returns appropriate response
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        // Extract correlation ID from context if available
        var correlationId = httpContext.Items["CorrelationId"]?.ToString();

        // Log the exception with correlation ID
        _logger.LogError(
            exception,
            "Unhandled exception occurred | CorrelationId: {CorrelationId} | Message: {Message}",
            correlationId,
            exception.Message);

        // Map exception to response
        var (statusCode, errorCode, message) = MapExceptionToResponse(exception);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var response = ApiResponse<object>.FailureResponse(
            new List<ApiError> { new(message, errorCode) },
            correlationId);

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);

        return true;
    }

    /// <summary>
    /// Maps exception types to appropriate HTTP status codes and error codes
    /// </summary>
    /// <remarks>
    /// OWASP Compliance:
    /// - A01 (Broken Access Control): Unauthorized → 401, Forbidden → 403
    /// - A02 (Cryptographic Failures): Authentication failures logged securely
    /// - A03 (Injection): Validation errors → 400
    /// - A07 (Identification Failures): Token validation failures → 401
    /// </remarks>
    private (int statusCode, string errorCode, string message) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            // Validation exceptions (400 Bad Request)
            FluentValidation.ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                FormatValidationErrors(validationEx)),

            // Domain exceptions with specific handling
            FileNotFoundException => (
                StatusCodes.Status404NotFound,
                "FILE_NOT_FOUND",
                "The requested file was not found."),

            Application.Exceptions.InvalidFileTypeException => (
                StatusCodes.Status400BadRequest,
                "INVALID_FILE_TYPE",
                "The uploaded file type is not supported. Only CNAB files are allowed."),

            Application.Exceptions.FileTooLargeException => (
                StatusCodes.Status400BadRequest,
                "FILE_TOO_LARGE",
                "The uploaded file exceeds the maximum size limit of 10MB."),

            Application.Exceptions.InvalidFileNameException => (
                StatusCodes.Status400BadRequest,
                "INVALID_FILE_NAME",
                "The file name contains invalid characters."),

            // Application exceptions
            ApplicationException appEx when appEx.Message.Contains("not found") => (
                StatusCodes.Status404NotFound,
                "NOT_FOUND",
                appEx.Message),

            ApplicationException appEx when appEx.Message.Contains("invalid") => (
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                appEx.Message),

            ApplicationException appEx => (
                StatusCodes.Status500InternalServerError,
                "APPLICATION_ERROR",
                appEx.Message),

            // Operational exceptions
            OperationCanceledException => (
                StatusCodes.Status408RequestTimeout,
                "REQUEST_TIMEOUT",
                "The request was cancelled or timed out."),

            // Default: Internal server error (500)
            _ => (
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred. Please contact support if the problem persists.")
        };
    }

    /// <summary>
    /// Formats validation error details into a user-friendly message
    /// </summary>
    private string FormatValidationErrors(FluentValidation.ValidationException exception)
    {
        var errors = exception.Errors;

        if (!errors.Any())
        {
            return "One or more validation errors occurred.";
        }

        // Return first error message as summary
        var firstError = errors.First();
        return firstError.ErrorMessage ?? "Validation failed.";
    }
}
