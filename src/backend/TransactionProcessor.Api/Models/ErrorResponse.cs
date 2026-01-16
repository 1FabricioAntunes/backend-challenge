namespace TransactionProcessor.Api.Models;

/// <summary>
/// Standardized error response model for API errors
/// Provides consistent error format across all endpoints
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error details
    /// </summary>
    public ErrorDetail Error { get; set; } = new();

    /// <summary>
    /// Creates a new error response with the given details
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="code">Error code (e.g., "FILE_NOT_FOUND")</param>
    /// <param name="statusCode">HTTP status code</param>
    public ErrorResponse(string message, string code, int statusCode)
    {
        Error = new ErrorDetail
        {
            Message = message,
            Code = code,
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public ErrorResponse() { }
}

/// <summary>
/// Error detail information
/// </summary>
public class ErrorDetail
{
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Machine-readable error code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }
}
