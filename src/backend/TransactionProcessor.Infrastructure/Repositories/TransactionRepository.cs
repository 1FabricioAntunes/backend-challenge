using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Transaction entity using Entity Framework Core
/// Normalized schema: BIGSERIAL ID, DateOnly+TimeOnly split, FK to transaction_types
/// </summary>
public class TransactionRepository : ITransactionRepository
{
    private readonly ApplicationDbContext _context;

    public TransactionRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get transaction by BIGSERIAL ID
    /// Note: ID is now long (BIGSERIAL), not Guid
    /// </summary>
    public async Task<Transaction?> GetByIdAsync(long id)
    {
        return await _context.Transactions
            .Include(t => t.File)
            .Include(t => t.Store)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// Get all transactions for a file
    /// Uses DateOnly for transactionDate (split from old OccurredAt)
    /// </summary>
    public async Task<IEnumerable<Transaction>> GetByFileIdAsync(Guid fileId)
    {
        return await _context.Transactions
            .AsNoTracking()
            .Where(t => t.FileId == fileId)
            .Include(t => t.Store)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();
    }

    /// <summary>
    /// Get transactions for store by date range
    /// Uses DateOnly for transaction_date filtering (normalized schema)
    /// </summary>
    public async Task<IEnumerable<Transaction>> GetByStoreIdAsync(Guid storeId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.Transactions
            .AsNoTracking()
            .Where(t => t.StoreId == storeId);

        if (startDate.HasValue)
        {
            var startDateOnly = DateOnly.FromDateTime(startDate.Value);
            query = query.Where(t => t.TransactionDate >= startDateOnly);
        }

        if (endDate.HasValue)
        {
            var endDateOnly = DateOnly.FromDateTime(endDate.Value);
            query = query.Where(t => t.TransactionDate <= endDateOnly);
        }

        return await query
            .OrderBy(t => t.TransactionDate)
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

    /// <summary>
    /// Check if transaction already exists for file+store combo
    /// Helps with idempotency during file processing
    /// </summary>
    public async Task<Transaction?> GetFirstByFileAndStoreAsync(Guid fileId, Guid storeId)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.FileId == fileId && t.StoreId == storeId);
    }
}
