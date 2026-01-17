using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Transaction entity using Entity Framework Core
/// </summary>
public class TransactionRepository : ITransactionRepository
{
    private readonly ApplicationDbContext _context;

    public TransactionRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        return await _context.Transactions
            .Include(t => t.File)
            .Include(t => t.Store)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Transaction>> GetByFileIdAsync(Guid fileId)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.FileId == fileId)
            .Include(t => t.Store)
            .OrderBy(t => t.OccurredAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Transaction>> GetByStoreIdAsync(Guid storeId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Transactions
            .AsNoTracking()
            .Where(t => t.StoreId == storeId);

        if (startDate.HasValue)
            query = query.Where(t => t.OccurredAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.OccurredAt <= endDate.Value);

        return await query
            .OrderBy(t => t.OccurredAt)
            .ToListAsync();
    }

    public async Task AddAsync(Transaction transaction)
    {
        await _context.Transactions.AddAsync(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Transaction> transactions)
    {
        await _context.Transactions.AddRangeAsync(transactions);
        await _context.SaveChangesAsync();
    }

    public async Task<Transaction?> GetFirstByFileAndStoreAsync(Guid fileId, Guid storeId)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.FileId == fileId && t.StoreId == storeId);
    }
}
