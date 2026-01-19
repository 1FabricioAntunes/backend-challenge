using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Application.UseCases.Stores.Queries;

/// <summary>
/// Handles store queries with balance computation using read-optimized projection.
/// </summary>
public class GetStoresQueryHandler : IRequestHandler<GetStoresQuery, List<StoreDto>>
{
    private readonly ApplicationDbContext _dbContext;

    public GetStoresQueryHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<List<StoreDto>> Handle(GetStoresQuery request, CancellationToken cancellationToken)
    {
        // Project stores with computed balances; uses AsNoTracking for read-only performance.
        var storeBalances = await _dbContext.Stores
            .AsNoTracking()
            .GroupJoin(
                _dbContext.Transactions
                    .AsNoTracking()
                    .Select(t => new
                    {
                        t.StoreId,
                        t.Amount,
                        Sign = t.TransactionType!.Sign
                    }),
                store => store.Id,
                tx => tx.StoreId,
                (store, tx) => new { store, tx })
            .Select(x => new StoreDto
            {
                Code = x.store.Id.ToString(),
                Name = x.store.Name,
                Balance = x.tx.Sum(t => (t.Amount / 100m) * (t.Sign == "+" ? 1m : -1m))
            })
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return storeBalances;
    }
}
