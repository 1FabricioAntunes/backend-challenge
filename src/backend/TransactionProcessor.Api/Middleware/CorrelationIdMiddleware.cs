using Serilog.Context;

namespace TransactionProcessor.Api.Middleware;

/// <summary>
/// Middleware to add correlation ID to all requests and responses
/// Enables request tracing across logs and multiple services
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-ID";

    /// <summary>
    /// Constructor
    /// </summary>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate or extract correlation ID
        var correlationId = ExtractOrGenerateCorrelationId(context);

        // Store in context for access by other middleware and handlers
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers for client to track
        context.Response.Headers.Append(CorrelationIdHeader, correlationId);

        // Add to Serilog context for structured logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogInformation(
                "Request started | Method: {Method} | Path: {Path} | CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            try
            {
                await _next(context);
            }
            finally
            {
                _logger.LogInformation(
                    "Request completed | Method: {Method} | Path: {Path} | StatusCode: {StatusCode} | CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    correlationId);
            }
        }
    }

    /// <summary>
    /// Extracts correlation ID from request headers or generates new one
    /// </summary>
    private string ExtractOrGenerateCorrelationId(HttpContext context)
    {
        // Try to extract from incoming request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationIdValue))
        {
            var correlationId = correlationIdValue.ToString();
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }
        }

        // Generate new correlation ID if not present
        return Guid.NewGuid().ToString("N");
    }
}

/// <summary>
/// Extension methods for correlation ID middleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    /// Adds correlation ID middleware to the request pipeline
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
