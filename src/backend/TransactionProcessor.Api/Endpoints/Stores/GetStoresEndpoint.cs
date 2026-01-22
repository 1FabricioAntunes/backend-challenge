using System.Diagnostics;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Application.UseCases.Stores.Queries;
using TransactionProcessor.Infrastructure.Metrics;

namespace TransactionProcessor.Api.Endpoints.Stores;

/// <summary>
/// Endpoint to retrieve all stores with their current balances.
/// GET /api/stores/v1
/// </summary>
[Authorize] // ✅ OWASP A01: Broken Access Control - Require JWT authentication
public class GetStoresEndpoint : EndpointWithoutRequest<List<StoreDto>>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetStoresEndpoint"/> class.
    /// </summary>
    /// <param name="mediator">The MediatR mediator for sending queries.</param>
    public GetStoresEndpoint(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Configures the endpoint.
    /// </summary>
    public override void Configure()
    {
        Get("/api/stores/v1");
        DontAutoTag(); // Prevent auto-tagging, use explicit tag only
        Description(d => d
            .Produces<List<StoreDto>>(200, "application/json")
            .WithTags("Stores")
            .WithSummary("Get all stores with balances")
            .WithDescription("Retrieves all stores with their current balances formatted as decimal (2 decimal places)"));
    }

    /// <summary>
    /// Handles the request to retrieve all stores with their balances.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A list of all stores with their current balances.</returns>
    public override async Task HandleAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var query = new GetStoresQuery();
            var result = await _mediator.Send(query, ct);

            stopwatch.Stop();

            // ========================================================================
            // METRICS: Record successful query
            // ========================================================================
            MetricsService.HttpRequestDurationSeconds
                .WithLabels("GET", "/api/stores/v1", "200")
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
                .WithLabels("GET", "/api/stores/v1", "500")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            Logger.LogError(ex, "Error retrieving stores");

            HttpContext.Response.StatusCode = 500;
            throw;
        }
    }
}
