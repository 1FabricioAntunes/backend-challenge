using TransactionProcessor.Domain.Entities;

namespace TransactionProcessor.Domain.Repositories;

/// <summary>
/// Repository interface for Store aggregate operations
/// </summary>
public interface IStoreRepository
{
    /// <summary>
    /// Get store by unique identifier
    /// </summary>
    /// <param name="id">Store identifier</param>
    /// <returns>Store entity or null if not found</returns>
    Task<Store?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get store by store code (unique)
    /// </summary>
    /// <param name="code">Store code from CNAB</param>
    /// <returns>Store entity or null if not found</returns>
    Task<Store?> GetByCodeAsync(string code);

    /// <summary>
    /// Get all stores
    /// </summary>
    /// <returns>Collection of all stores</returns>
    Task<IEnumerable<Store>> GetAllAsync();

    /// <summary>
    /// Add new store or update if exists
    /// </summary>
    /// <param name="store">Store entity to add or update</param>
    Task UpsertAsync(Store store);

    /// <summary>
    /// Update existing store
    /// </summary>
    /// <param name="store">Store entity with updated data</param>
    Task UpdateAsync(Store store);
}
