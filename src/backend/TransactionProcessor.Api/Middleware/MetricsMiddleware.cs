using System.Diagnostics;
using TransactionProcessor.Infrastructure.Metrics;

namespace TransactionProcessor.Api.Middleware;

/// <summary>
/// Middleware for recording HTTP request metrics (duration and count).
/// Records all HTTP requests to Prometheus metrics for observability.
/// </summary>
public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Exclude metrics endpoint to avoid recording its own metrics
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var method = context.Request.Method;
        var endpoint = context.Request.Path.Value ?? "/";
        
        // Normalize endpoint path to reduce cardinality (remove IDs, etc.)
        var normalizedEndpoint = NormalizeEndpoint(endpoint);

        // Track active requests (increment before processing)
        MetricsService.ActiveHttpRequests.WithLabels(method, normalizedEndpoint).Inc();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
            
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode.ToString();
            
            // Record request metrics
            MetricsService.RecordHttpRequest(method, normalizedEndpoint, statusCode);
            MetricsService.HttpRequestDurationSeconds
                .WithLabels(method, normalizedEndpoint)
                .Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogDebug(
                "HTTP request completed - Method: {Method}, Endpoint: {Endpoint}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                method, normalizedEndpoint, statusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Record error metrics
            MetricsService.RecordError("http_exception");
            MetricsService.HttpRequestDurationSeconds
                .WithLabels(method, normalizedEndpoint)
                .Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogError(
                ex,
                "HTTP request failed - Method: {Method}, Endpoint: {Endpoint}, Duration: {Duration}ms",
                method, normalizedEndpoint, stopwatch.ElapsedMilliseconds);

            throw;
        }
        finally
        {
            // Decrement active requests
            MetricsService.ActiveHttpRequests.WithLabels(method, normalizedEndpoint).Dec();
        }
    }

    /// <summary>
    /// Normalize endpoint path to reduce metric cardinality.
    /// Replaces GUIDs and numeric IDs with placeholders.
    /// Example: /api/files/550e8400-e29b-41d4-a716-446655440000 → /api/files/{id}
    /// </summary>
    private static string NormalizeEndpoint(string endpoint)
    {
        // Replace GUIDs with {id}
        endpoint = System.Text.RegularExpressions.Regex.Replace(
            endpoint,
            @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
            "{id}",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Replace numeric IDs with {id}
        endpoint = System.Text.RegularExpressions.Regex.Replace(
            endpoint,
            @"/\d+(?=/|$)",
            "/{id}");

        return endpoint;
    }
}

/// <summary>
/// Extension methods for registering metrics middleware
/// </summary>
public static class MetricsMiddlewareExtensions
{
    /// <summary>
    /// Add metrics middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseMetrics(this IApplicationBuilder app)
    {
        return app.UseMiddleware<MetricsMiddleware>();
    }
}
