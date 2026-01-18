namespace TransactionProcessor.Domain.Repositories;

/// <summary>
/// Base repository interface for generic CRUD operations.
/// 
/// Provides a consistent API across all repository implementations.
/// Specific repositories (IFileRepository, IStoreRepository, etc.) can extend this
/// interface with domain-specific methods while inheriting standard CRUD operations.
/// 
/// Type Parameter Constraints:
/// - TEntity: Domain entity type (File, Store, Transaction, etc.)
/// - TId: Primary key type (Guid for File/Store, long for Transaction)
/// 
/// Design Benefits:
/// - Consistency: All repositories share common method signatures
/// - Maintainability: Changes to base operations propagate to all repositories
/// - Testability: Mock implementations can reuse base interface
/// - Extensibility: Specific repositories add domain-specific methods
/// 
/// Usage Pattern:
/// <code>
/// public interface IFileRepository : IRepository&lt;File, Guid&gt;
/// {
///     // Domain-specific methods
///     Task&lt;IEnumerable&lt;File&gt;&gt; GetPendingFilesAsync();
/// }
/// </code>
/// 
/// Reference: Technical Decisions § 4.1 (Clean Architecture)
/// Reference: docs/architecture.md § Infrastructure Layer
/// </summary>
/// <typeparam name="TEntity">Domain entity type</typeparam>
/// <typeparam name="TId">Primary key type</typeparam>
public interface IRepository<TEntity, in TId> where TEntity : class
{
    /// <summary>
    /// Retrieve entity by unique identifier.
    /// 
    /// Should return null if entity not found.
    /// Implementations may choose to eager-load related entities as needed.
    /// </summary>
    /// <param name="id">Entity identifier</param>
    /// <returns>Entity or null if not found</returns>
    Task<TEntity?> GetByIdAsync(TId id);

    /// <summary>
    /// Retrieve all entities of this type.
    /// 
    /// CAUTION: May return large result sets.
    /// Consider using pagination or filtering for production queries.
    /// 
    /// PERFORMANCE OPTIMIZATION:
    /// Implementations should use AsNoTracking() for read-only queries.
    /// </summary>
    /// <returns>Collection of all entities</returns>
    Task<IEnumerable<TEntity>> GetAllAsync();

    /// <summary>
    /// Add a new entity to the repository.
    /// 
    /// Business rules enforced by entity constructor and domain invariants.
    /// Primary key may be auto-generated (BIGSERIAL) or provided (UUID).
    /// </summary>
    /// <param name="entity">Entity to add</param>
    /// <exception cref="ArgumentNullException">Thrown if entity is null</exception>
    Task AddAsync(TEntity entity);

    /// <summary>
    /// Update an existing entity.
    /// 
    /// EF Core tracks changes and updates only modified fields.
    /// Primary key is immutable and cannot be changed.
    /// </summary>
    /// <param name="entity">Entity with updated data</param>
    /// <exception cref="ArgumentNullException">Thrown if entity is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if entity does not exist</exception>
    Task UpdateAsync(TEntity entity);

    /// <summary>
    /// Delete an entity by identifier.
    /// 
    /// Foreign key constraints may prevent deletion if entity is referenced.
    /// Consider soft deletes for entities with audit trail requirements.
    /// </summary>
    /// <param name="id">Entity identifier</param>
    /// <exception cref="InvalidOperationException">Thrown if entity does not exist or has dependencies</exception>
    Task DeleteAsync(TId id);
}
