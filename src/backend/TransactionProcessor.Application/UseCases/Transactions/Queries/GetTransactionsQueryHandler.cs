using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Application.UseCases.Transactions.Queries;

/// <summary>
/// Handles transaction querying with filters and pagination.
/// Applies read optimizations: AsNoTracking, projection, and ordered pagination.
/// </summary>
public class GetTransactionsQueryHandler : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionDto>>
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 500;

    private readonly ApplicationDbContext _dbContext;
    private readonly ITransactionRepository _transactionRepository; // kept for future reuse/consistency

    public GetTransactionsQueryHandler(ApplicationDbContext dbContext, ITransactionRepository transactionRepository)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
    }

    public async Task<PagedResult<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        if (request.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(request.Page), "Page must be >= 1");

        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            throw new ArgumentException("StartDate must be less than or equal to EndDate.");

        // Base query with read-optimized settings and necessary relationships for projection
        var query = _dbContext.Transactions
            .AsNoTracking()
            // Include navigation sets for projection; EF will translate projection without materializing full graphs
            .Include(t => t.Store)
            .Include(t => t.TransactionType)
            .AsQueryable();

        if (request.StoreId.HasValue)
        {
            query = query.Where(t => t.StoreId == request.StoreId.Value);
        }

        if (request.StartDate.HasValue)
        {
            var startDateOnly = DateOnly.FromDateTime(request.StartDate.Value.Date);
            query = query.Where(t => t.TransactionDate >= startDateOnly);
        }

        if (request.EndDate.HasValue)
        {
            var endDateOnly = DateOnly.FromDateTime(request.EndDate.Value.Date);
            query = query.Where(t => t.TransactionDate <= endDateOnly);
        }

        // Total count for pagination metadata
        var totalCount = await query.CountAsync(cancellationToken);

        // Order by most recent first (date desc, time desc)
        query = query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.TransactionTime);

        // Paging
        var skip = (request.Page - 1) * pageSize;

        // Project directly to DTO to reduce memory and avoid tracking
        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                StoreCode = t.StoreId.ToString(),
                StoreName = t.Store != null ? t.Store.Name : string.Empty,
                Type = t.TransactionTypeCode,
                // Signed amount in BRL using lookup sign (TransactionType.Sign)
                Amount = (t.Amount / 100m) * (t.TransactionType!.Sign == "+" ? 1m : -1m),
                // Combine DateOnly + TimeOnly as UTC DateTime for ISO 8601 serialization
                Date = DateTime.SpecifyKind(t.TransactionDate.ToDateTime(t.TransactionTime), DateTimeKind.Utc)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TransactionDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
