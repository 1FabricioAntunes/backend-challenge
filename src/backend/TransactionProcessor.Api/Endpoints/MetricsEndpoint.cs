using FastEndpoints;
using Prometheus;

namespace TransactionProcessor.Api.Endpoints;

/// <summary>
/// Metrics endpoint for Prometheus scraping.
/// Returns metrics in Prometheus text format.
/// Endpoint: GET /api/metrics
/// </summary>
public class MetricsEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/metrics");
        AllowAnonymous();  // Metrics endpoint should not require authentication
        Tags("Observability");
        Summary(s => s.Summary = "Prometheus metrics endpoint");
        Description(d => d.WithDescription("Returns metrics in Prometheus text format for scraping"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Set response content type for Prometheus (text format 0.0.4)
        HttpContext.Response.ContentType = PrometheusConstants.TextContentType;
        
        // Use built-in prometheus-net serializer for correct text format
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(
            HttpContext.Response.Body,
            ct);
    }
}

/// <summary>
/// Simple health check endpoint for monitoring
/// Endpoint: GET /api/health
/// </summary>
public class HealthCheckEndpoint : EndpointWithoutRequest<HealthCheckResponse>
{
    public override void Configure()
    {
        Get("/api/health");
        AllowAnonymous();
        Tags("Observability");
        Summary(s => s.Summary = "Health check endpoint");
        Description(d => d.WithDescription("Returns application health status"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        Response = new HealthCheckResponse
        {
            Status = "healthy",
            Timestamp = DateTimeOffset.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Response model for health check endpoint
/// </summary>
public class HealthCheckResponse
{
    /// <summary>
    /// Health status (healthy, degraded, unhealthy)
    /// </summary>
    public string Status { get; set; } = "healthy";

    /// <summary>
    /// Timestamp of health check
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Application environment
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Application version
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Application name
    /// </summary>
    public string Application { get; set; } = "TransactionProcessor";
}

