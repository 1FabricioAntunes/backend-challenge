using MediatR;
using TransactionProcessor.Application.DTOs;

namespace TransactionProcessor.Application.UseCases.Transactions.Queries;

/// <summary>
/// Query to retrieve transactions with optional filters and pagination.
/// </summary>
public class GetTransactionsQuery : IRequest<PagedResult<TransactionDto>>
{
    /// <summary>
    /// Optional filter by Store Id.
    /// </summary>
    public Guid? StoreId { get; init; }

    /// <summary>
    /// Optional start date (inclusive, UTC).
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Optional end date (inclusive, UTC).
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Page number (1-based). Defaults to 1.
    /// </summary>
    public int Page { get; init; } = 1;

    /// <summary>
    /// Page size. Defaults to 50. Max 500.
    /// </summary>
    public int PageSize { get; init; } = 50;
}
