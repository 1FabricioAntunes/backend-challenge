using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Store aggregate using Entity Framework Core
/// </summary>
public class StoreRepository : IStoreRepository
{
    private readonly ApplicationDbContext _context;

    public StoreRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Store?> GetByIdAsync(Guid id)
    {
        return await _context.Stores
            .Include(s => s.Transactions)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Store?> GetByCodeAsync(string code)
    {
        return await _context.Stores
            .FirstOrDefaultAsync(s => s.Code == code);
    }

    public async Task<IEnumerable<Store>> GetAllAsync()
    {
        return await _context.Stores
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task UpsertAsync(Store store)
    {
        var existing = await _context.Stores
            .FirstOrDefaultAsync(s => s.Code == store.Code);

        if (existing != null)
        {
            existing.Name = store.Name;
            existing.Balance = store.Balance;
            existing.UpdatedAt = store.UpdatedAt;
            _context.Stores.Update(existing);
        }
        else
        {
            await _context.Stores.AddAsync(store);
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Store store)
    {
        _context.Stores.Update(store);
        await _context.SaveChangesAsync();
    }
}
