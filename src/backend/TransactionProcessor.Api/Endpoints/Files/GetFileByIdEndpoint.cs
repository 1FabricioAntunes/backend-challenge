using FastEndpoints;
using MediatR;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Application.Queries.Files;
using ApiErrorResponse = TransactionProcessor.Api.Models.ErrorResponse;

namespace TransactionProcessor.Api.Endpoints.Files;

/// <summary>
/// Request model for getting file by ID
/// </summary>
public class GetFileByIdRequest
{
    /// <summary>
    /// Unique identifier of the file
    /// </summary>
    public Guid Id { get; set; }
}

/// <summary>
/// Endpoint to retrieve a single file by ID
/// GET /api/files/v1/{id}
/// </summary>
public class GetFileByIdEndpoint : Endpoint<GetFileByIdRequest, FileDto>
{
    private readonly IMediator _mediator;

    public GetFileByIdEndpoint(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Get("/api/files/v1/{id}");
        AllowAnonymous();
        Description(d => d
            .Produces<FileDto>(200, "application/json")
            .Produces<ApiErrorResponse>(404, "application/json")
            .Produces<ApiErrorResponse>(400, "application/json")
            .WithTags("Files")
            .WithSummary("Get file by ID")
            .WithDescription("Retrieves detailed information about a specific file including processing status"));
    }

    public override async Task HandleAsync(GetFileByIdRequest req, CancellationToken ct)
    {
        // Validate file ID
        if (req.Id == Guid.Empty)
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse(
                    "File ID cannot be empty.",
                    "INVALID_FILE_ID",
                    400),
                cancellationToken: ct);
            return;
        }

        try
        {
            // Execute query via MediatR
            var query = new GetFileByIdQuery(req.Id);
            var result = await _mediator.Send(query, ct);

            // Return 404 if file not found
            if (result == null)
            {
                HttpContext.Response.StatusCode = 404;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        $"File with ID '{req.Id}' was not found.",
                        "FILE_NOT_FOUND",
                        404),
                    cancellationToken: ct);
                return;
            }

            HttpContext.Response.StatusCode = 200;
            await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            HttpContext.Response.StatusCode = 500;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse(
                    "An error occurred while retrieving the file.",
                    "INTERNAL_ERROR",
                    500),
                cancellationToken: ct);
            
            // Log exception (structured logging should be configured)
            Console.Error.WriteLine($"Error in GetFileByIdEndpoint: {ex}");
        }
    }
}
