using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Store aggregate using Entity Framework Core
/// Normalized schema: composite unique key (Name, OwnerName), no persisted Balance
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
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    /// <summary>
    /// Get store by composite key (Name, OwnerName) - normalized approach
    /// </summary>
    /// <param name="name">Store name</param>
    /// <param name="ownerName">Store owner name</param>
    /// <returns>Store entity or null if not found</returns>
    public async Task<Store?> GetByNameAndOwnerAsync(string name, string ownerName)
    {
        return await _context.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name && s.OwnerName == ownerName);
    }

    public async Task<IEnumerable<Store>> GetAllAsync()
    {
        // Read-only optimization: AsNoTracking()
        // Do not eager-load Transactions to reduce data transfer
        // Returns Store entities without Transactions collection populated
        return await _context.Stores
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Upsert store using composite key (Name, OwnerName)
    /// Note: Balance is not persisted; compute from transactions using GetSignedAmount()
    /// </summary>
    /// <param name="store">Store entity to create or update</param>
    public async Task AddAsync(Store store)
    {
        if (store == null)
            throw new ArgumentNullException(nameof(store));

        await _context.Stores.AddAsync(store);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Store store)
    {
        if (store == null)
            throw new ArgumentNullException(nameof(store));

        _context.Stores.Update(store);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateBalanceAsync(Guid storeId, decimal newBalance)
    {
        if (newBalance < 0)
            throw new ArgumentException("Balance cannot be negative.", nameof(newBalance));

        var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId);
        if (store == null)
            throw new InvalidOperationException("Store not found.");

        store.UpdateBalance(newBalance);
        _context.Stores.Update(store);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Legacy method for backwards compatibility - kept but deprecated
    /// Use GetByNameAndOwnerAsync instead for normalized schema
    /// </summary>
    public async Task<Store?> GetByCodeAsync(string code)
    {
        // Not supported in normalized schema (composite key: Name + OwnerName)
        // Kept for backwards compatibility with older callers.
        // If a Code column is introduced, implement lookup here.
        return await Task.FromResult<Store?>(null);
    }
}
