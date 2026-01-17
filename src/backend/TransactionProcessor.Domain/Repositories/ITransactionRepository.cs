using TransactionProcessor.Domain.Entities;

namespace TransactionProcessor.Domain.Repositories;

/// <summary>
/// Repository interface for Transaction entity operations
/// Normalized schema: ID is BIGSERIAL (long), not UUID
/// </summary>
public interface ITransactionRepository
{
    /// <summary>
    /// Get transaction by unique identifier (BIGSERIAL ID as long)
    /// </summary>
    /// <param name="id">Transaction identifier (BIGSERIAL)</param>
    /// <returns>Transaction entity or null if not found</returns>
    Task<Transaction?> GetByIdAsync(long id);

    /// <summary>
    /// Get all transactions for a file
    /// </summary>
    /// <param name="fileId">File identifier</param>
    /// <returns>Collection of transactions for the file</returns>
    Task<IEnumerable<Transaction>> GetByFileIdAsync(Guid fileId);

    /// <summary>
    /// Get transactions for a store within a date range
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="startDate">Start date (inclusive), null for no start limit</param>
    /// <param name="endDate">End date (inclusive), null for no end limit</param>
    /// <returns>Collection of transactions</returns>
    Task<IEnumerable<Transaction>> GetByStoreIdAsync(Guid storeId, DateTime? startDate = null, DateTime? endDate = null);

    /// <summary>
    /// Add new transaction
    /// </summary>
    /// <param name="transaction">Transaction entity to add</param>
    Task AddAsync(Transaction transaction);

    /// <summary>
    /// Add multiple transactions (batch insert)
    /// </summary>
    /// <param name="transactions">Collection of transactions to add</param>
    Task AddRangeAsync(IEnumerable<Transaction> transactions);

    /// <summary>
    /// Get transaction by file and store ID to check idempotency
    /// </summary>
    /// <param name="fileId">File identifier</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>First transaction or null</returns>
    Task<Transaction?> GetFirstByFileAndStoreAsync(Guid fileId, Guid storeId);
}
