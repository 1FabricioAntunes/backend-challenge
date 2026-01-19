namespace TransactionProcessor.Api.Models;

/// <summary>
/// Standardized API response model for all endpoints
/// Provides consistent response format with data, success flag, errors, and timestamp
/// </summary>
/// <typeparam name="T">Type of the response data</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// Response data payload
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Indicates if the request was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// List of errors (null if successful)
    /// </summary>
    public List<ApiError>? Errors { get; set; }

    /// <summary>
    /// Timestamp of the response (ISO 8601 UTC format)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for request tracing across logs
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static ApiResponse<T> SuccessResponse(T data, string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Data = data,
            Success = true,
            Errors = null,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Creates a failure response with errors
    /// </summary>
    public static ApiResponse<T> FailureResponse(List<ApiError> errors, string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Data = default,
            Success = false,
            Errors = errors,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Creates a failure response with a single error
    /// </summary>
    public static ApiResponse<T> FailureResponse(string message, string code, string? correlationId = null)
    {
        return FailureResponse(new List<ApiError> { new(message, code) }, correlationId);
    }
}

/// <summary>
/// Represents a single error in the API response
/// </summary>
public class ApiError
{
    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Machine-readable error code (e.g., "FILE_NOT_FOUND", "VALIDATION_ERROR")
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Optional field name for validation errors
    /// </summary>
    public string? Field { get; set; }

    /// <summary>
    /// Constructor
    /// </summary>
    public ApiError(string message, string code, string? field = null)
    {
        Message = message;
        Code = code;
        Field = field;
    }
}

/// <summary>
/// Non-generic API response for endpoints that don't return data
/// </summary>
public class ApiResponse
{
    /// <summary>
    /// Indicates if the request was successful
    /// </summary>
    public bool Success { get; set; } = true;

    /// <summary>
    /// List of errors (null if successful)
    /// </summary>
    public List<ApiError>? Errors { get; set; }

    /// <summary>
    /// Timestamp of the response (ISO 8601 UTC format)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Correlation ID for request tracing across logs
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Creates a successful response
    /// </summary>
    public static ApiResponse SuccessResponse(string? correlationId = null)
    {
        return new ApiResponse
        {
            Success = true,
            Errors = null,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Creates a failure response with errors
    /// </summary>
    public static ApiResponse FailureResponse(List<ApiError> errors, string? correlationId = null)
    {
        return new ApiResponse
        {
            Success = false,
            Errors = errors,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };
    }
}
