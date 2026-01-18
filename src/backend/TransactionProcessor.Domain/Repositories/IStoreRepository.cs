using TransactionProcessor.Domain.Entities;

namespace TransactionProcessor.Domain.Repositories;

/// <summary>
/// Repository interface for Store aggregate root operations.
/// 
/// Defines the contract for persisting and retrieving Store aggregates.
/// Implementations should handle transactional consistency and enforce invariants.
/// 
/// Reference: Technical Decisions § 4.2 (Domain-Driven Design) and
/// docs/architecture.md § Infrastructure Layer for DDD repository patterns.
/// </summary>
public interface IStoreRepository
{
    /// <summary>
    /// Retrieve store by unique identifier.
    /// 
    /// This method should eager-load the Transactions collection for balance calculations
    /// and other aggregate operations.
    /// </summary>
    /// <param name="id">Store identifier (UUID v7)</param>
    /// <returns>Store entity with transactions loaded, or null if not found</returns>
    Task<Store?> GetByIdAsync(Guid id);

    /// <summary>
    /// Retrieve store by composite unique key (Name + OwnerName).
    /// 
    /// Supports idempotent upsert operations during CNAB file processing.
    /// The combination of Name and OwnerName uniquely identifies a store in CNAB format.
    /// 
    /// Reference: docs/business-rules.md for CNAB store identification.
    /// </summary>
    /// <param name="name">Store name from CNAB (max 19 characters)</param>
    /// <param name="ownerName">Store owner name from CNAB (max 14 characters)</param>
    /// <returns>Store entity or null if not found</returns>
    Task<Store?> GetByNameAndOwnerAsync(string name, string ownerName);

    /// <summary>
    /// Retrieve all stores in the system.
    /// 
    /// Should use AsNoTracking() for read-only performance optimization.
    /// Do not load full Transaction collections; use projections for large result sets.
    /// </summary>
    /// <returns>Collection of all Store entities</returns>
    Task<IEnumerable<Store>> GetAllAsync();

    /// <summary>
    /// Add a new store to the repository.
    /// 
    /// Business rules:
    /// - Store must have valid Name and OwnerName (enforced by Store constructor)
    /// - Store.Id must be unique (immutable UUID v7)
    /// - Composite key (Name, OwnerName) must be unique within CNAB context
    /// 
    /// Implementation should enforce uniqueness constraints at database level.
    /// </summary>
    /// <param name="store">New Store aggregate to persist</param>
    /// <exception cref="ArgumentNullException">Thrown if store is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if store with same Id or (Name, OwnerName) already exists</exception>
    Task AddAsync(Store store);

    /// <summary>
    /// Update an existing store.
    /// 
    /// Business rules:
    /// - Store.Id is immutable and cannot be changed
    /// - Store.OwnerName and Store.Name define the composite key and should rarely change
    /// - Store.Balance is calculated from transactions, not directly updated (use UpdateBalanceAsync instead)
    /// - UpdatedAt timestamp must be set to current UTC time
    /// 
    /// Implementation should enforce that only the permitted fields are updated.
    /// </summary>
    /// <param name="store">Store aggregate with updated data</param>
    /// <exception cref="ArgumentNullException">Thrown if store is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if store does not exist</exception>
    Task UpdateAsync(Store store);

    /// <summary>
    /// Update store balance after transaction processing.
    /// 
    /// This method specifically handles balance updates during file processing.
    /// It ensures that balance changes are transactional and auditable.
    /// 
    /// Business rule: Balance cannot be negative (validated by Store.UpdateBalance).
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="newBalance">New balance in BRL</param>
    /// <exception cref="ArgumentException">Thrown if newBalance is negative</exception>
    /// <exception cref="InvalidOperationException">Thrown if store does not exist</exception>
    Task UpdateBalanceAsync(Guid storeId, decimal newBalance);
}
