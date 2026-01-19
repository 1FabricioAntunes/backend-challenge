using FastEndpoints;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Application.UseCases.Transactions.Queries;
using TransactionProcessor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using ApiErrorResponse = TransactionProcessor.Api.Models.ErrorResponse;

namespace TransactionProcessor.Api.Endpoints.Transactions;

public class TransactionsQueryRequest
{
    public Guid? StoreId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public class GetTransactionsEndpoint : Endpoint<TransactionsQueryRequest, PagedResult<TransactionDto>>
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _db;

    public GetTransactionsEndpoint(IMediator mediator, ApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/transactions/v1");
        AllowAnonymous();
        Description(b => b.Produces(200)
            .Produces<ApiErrorResponse>(400)
            .Produces<ApiErrorResponse>(404));
    }

    public override async Task HandleAsync(TransactionsQueryRequest req, CancellationToken ct)
    {
        // Basic parameter validation → 400
        var errors = new List<string>();
        if (req.Page < 1)
            errors.Add("'page' must be >= 1.");
        if (req.PageSize < 1 || req.PageSize > 500)
            errors.Add("'pageSize' must be between 1 and 500.");
        if (req.StartDate.HasValue && req.EndDate.HasValue && req.StartDate.Value > req.EndDate.Value)
            errors.Add("'startDate' must be <= 'endDate'.");

        if (errors.Count > 0)
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsJsonAsync(
                new ApiErrorResponse(string.Join(" ", errors), "INVALID_PARAMETERS", 400),
                cancellationToken: ct);
            return;
        }

        // If filtering by StoreId, verify it exists → 404
        if (req.StoreId.HasValue)
        {
            var exists = await _db.Stores.AsNoTracking().AnyAsync(s => s.Id == req.StoreId.Value, ct);
            if (!exists)
            {
                HttpContext.Response.StatusCode = 404;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse("Store not found.", "STORE_NOT_FOUND", 404),
                    cancellationToken: ct);
                return;
            }
        }

        var query = new GetTransactionsQuery
        {
            StoreId = req.StoreId,
            StartDate = req.StartDate?.ToUniversalTime(),
            EndDate = req.EndDate?.ToUniversalTime(),
            Page = req.Page <= 0 ? 1 : req.Page,
            PageSize = req.PageSize <= 0 ? 50 : Math.Min(req.PageSize, 500)
        };

        var result = await _mediator.Send(query, ct);
        HttpContext.Response.StatusCode = 200;
        await HttpContext.Response.WriteAsJsonAsync(result, cancellationToken: ct);
    }
}
