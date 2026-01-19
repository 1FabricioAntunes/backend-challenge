using FastEndpoints;
using MediatR;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Application.UseCases.Stores.Queries;

namespace TransactionProcessor.Api.Endpoints.Stores;

/// <summary>
/// Endpoint to retrieve all stores with their current balances.
/// GET /api/stores/v1
/// </summary>
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
        AllowAnonymous();
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
        var query = new GetStoresQuery();
        var result = await _mediator.Send(query, ct);

        HttpContext.Response.StatusCode = 200;
        await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
    }
}
