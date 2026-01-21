using System.Diagnostics;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Application.Queries.Files;
using TransactionProcessor.Infrastructure.Metrics;
using ApiErrorResponse = TransactionProcessor.Api.Models.ErrorResponse;

namespace TransactionProcessor.Api.Endpoints.Files;

/// <summary>
/// Request model for paginated file list
/// </summary>
public class GetFilesRequest
{
    /// <summary>
    /// Page number (1-based indexing, minimum: 1)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page (minimum: 1, maximum: 100)
    /// </summary>
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Endpoint to retrieve paginated list of files
/// GET /api/files/v1
/// </summary>
[Authorize] // ✅ OWASP A01: Broken Access Control - Require JWT authentication
public class GetFilesEndpoint : Endpoint<GetFilesRequest, PagedResult<FileDto>>
{
    private readonly IMediator _mediator;

    public GetFilesEndpoint(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Get("/api/files/v1");
        DontAutoTag(); // Prevent auto-tagging, use explicit tag only
        Description(d => d
            .Produces<PagedResult<FileDto>>(200, "application/json")
            .Produces<ApiErrorResponse>(400, "application/json")
            .WithTags("Files")
            .WithSummary("Get paginated list of files")
            .WithDescription("Retrieves a paginated list of uploaded files with their processing status"));
    }

    public override async Task HandleAsync(GetFilesRequest req, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        // Validate pagination parameters
        if (req.Page < 1)
        {
            stopwatch.Stop();

            // ========================================================================
            // METRICS: Record validation error
            // ========================================================================
            MetricsService.RecordError("invalid_page_number");
            MetricsService.HttpRequestDurationSeconds
                .WithLabels("GET", "/api/files/v1", "400")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse(
                    "Page number must be greater than or equal to 1.",
                    "INVALID_PAGE_NUMBER",
                    400),
                cancellationToken: ct);
            return;
        }

        if (req.PageSize < 1 || req.PageSize > 100)
        {
            stopwatch.Stop();

            // ========================================================================
            // METRICS: Record validation error
            // ========================================================================
            MetricsService.RecordError("invalid_page_size");
            MetricsService.HttpRequestDurationSeconds
                .WithLabels("GET", "/api/files/v1", "400")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse(
                    "Page size must be between 1 and 100.",
                    "INVALID_PAGE_SIZE",
                    400),
                cancellationToken: ct);
            return;
        }

        try
        {
            // Execute query via MediatR
            var query = new GetFilesQuery(req.Page, req.PageSize);
            var result = await _mediator.Send(query, ct);

            stopwatch.Stop();

            // ========================================================================
            // METRICS: Record successful query
            // ========================================================================
            MetricsService.HttpRequestDurationSeconds
                .WithLabels("GET", "/api/files/v1", "200")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            HttpContext.Response.StatusCode = 200;
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // ========================================================================
            // METRICS: Record error
            // ========================================================================
            MetricsService.RecordError("unhandled_exception");
            MetricsService.HttpRequestDurationSeconds
                .WithLabels("GET", "/api/files/v1", "500")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            HttpContext.Response.StatusCode = 500;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse(
                    "An error occurred while retrieving files.",
                    "INTERNAL_ERROR",
                    500),
                cancellationToken: ct);
            
            // Log exception (structured logging should be configured)
            Console.Error.WriteLine($"Error in GetFilesEndpoint: {ex}");
        }
    }
}
