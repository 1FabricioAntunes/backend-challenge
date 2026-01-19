using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Application.DTOs;
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

    public GetTransactionsQueryHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PagedResult<TransactionDto>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            throw new ArgumentException("StartDate must be less than or equal to EndDate.");

        // Base query with read-optimized settings and necessary relationships for projection
        var query = _dbContext.Transactions
            .AsNoTracking()
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

        var totalCount = await query.CountAsync(cancellationToken);

        query = query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.TransactionTime);

        var skip = (page - 1) * pageSize;

        var items = await query
            .Skip(skip)
            .Take(pageSize)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                StoreCode = t.StoreId.ToString(),
                StoreName = t.Store != null ? t.Store.Name : string.Empty,
                Type = t.TransactionTypeCode,
                Amount = t.TransactionType != null
                    ? (t.Amount / 100m) * (t.TransactionType.Sign == "+" ? 1m : -1m)
                    : 0m,
                Date = DateTime.SpecifyKind(t.TransactionDate.ToDateTime(t.TransactionTime), DateTimeKind.Utc)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TransactionDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
