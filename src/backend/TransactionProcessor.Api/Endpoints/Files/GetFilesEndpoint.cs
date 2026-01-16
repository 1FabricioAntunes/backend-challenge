using FastEndpoints;
using MediatR;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Application.Queries.Files;
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
        AllowAnonymous();
        Description(d => d
            .Produces<PagedResult<FileDto>>(200, "application/json")
            .Produces<ApiErrorResponse>(400, "application/json")
            .WithTags("Files")
            .WithSummary("Get paginated list of files")
            .WithDescription("Retrieves a paginated list of uploaded files with their processing status"));
    }

    public override async Task HandleAsync(GetFilesRequest req, CancellationToken ct)
    {
        // Validate pagination parameters
        if (req.Page < 1)
        {
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

            HttpContext.Response.StatusCode = 200;
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (Exception ex)
        {
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
