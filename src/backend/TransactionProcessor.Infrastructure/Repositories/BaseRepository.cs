using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;

namespace TransactionProcessor.Infrastructure.Repositories;

/// <summary>
/// Abstract base repository implementation with common CRUD operations.
/// 
/// Provides default implementations for standard repository methods using EF Core.
/// Specific repositories (FileRepository, StoreRepository, etc.) can inherit from this
/// base class and override methods as needed for domain-specific behavior.
/// 
/// EF Core Optimization Patterns:
/// - AsNoTracking() for all read-only queries (reduces memory usage by 30-40%)
/// - Parameterized queries to prevent SQL injection
/// - Async operations for scalability
/// 
/// Type Parameter Constraints:
/// - TEntity: Domain entity type (must be a class)
/// - TId: Primary key type (Guid, long, int, etc.)
/// 
/// Design Benefits:
/// - Code Reuse: Common CRUD logic implemented once
/// - Consistency: All repositories use same patterns
/// - Maintainability: Changes to base operations propagate automatically
/// - Extensibility: Override methods for custom behavior
/// 
/// Usage Pattern:
/// <code>
/// public class FileRepository : BaseRepository&lt;File, Guid&gt;, IFileRepository
/// {
///     public FileRepository(ApplicationDbContext context) : base(context) { }
///     
///     // Override base methods if needed
///     public override async Task&lt;File?&gt; GetByIdAsync(Guid id)
///     {
///         return await Context.Files
///             .Include(f => f.Transactions)
///             .FirstOrDefaultAsync(f => f.Id == id);
///     }
///     
///     // Add domain-specific methods
///     public async Task&lt;IEnumerable&lt;File&gt;&gt; GetPendingFilesAsync() { ... }
/// }
/// </code>
/// 
/// Reference: Technical Decisions § 4.1 (Clean Architecture)
/// Reference: Technical Decisions § 7 (EF Core Optimizations)
/// Reference: docs/database.md § EF Core Optimization Patterns
/// </summary>
/// <typeparam name="TEntity">Domain entity type</typeparam>
/// <typeparam name="TId">Primary key type</typeparam>
public abstract class BaseRepository<TEntity, TId> : IRepository<TEntity, TId> 
    where TEntity : class
{
    /// <summary>
    /// EF Core database context.
    /// Protected to allow derived repositories to access DbContext for custom queries.
    /// </summary>
    protected readonly ApplicationDbContext Context;

    /// <summary>
    /// DbSet for the entity type.
    /// Provides direct access to entity table for LINQ queries.
    /// </summary>
    protected readonly DbSet<TEntity> DbSet;

    /// <summary>
    /// Constructor for base repository.
    /// Initializes DbContext and DbSet references.
    /// </summary>
    /// <param name="context">Application database context</param>
    /// <exception cref="ArgumentNullException">Thrown if context is null</exception>
    protected BaseRepository(ApplicationDbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = context.Set<TEntity>();
    }

    /// <summary>
    /// Retrieve entity by unique identifier (base implementation).
    /// 
    /// Uses AsNoTracking() for read-only query optimization.
    /// Does not eager-load related entities (override if needed).
    /// 
    /// Derived repositories should override this method to:
    /// - Include related entities via Include()
    /// - Add custom filtering logic
    /// - Implement different tracking behavior
    /// </summary>
    /// <param name="id">Entity identifier</param>
    /// <returns>Entity or null if not found</returns>
    public virtual async Task<TEntity?> GetByIdAsync(TId id)
    {
        return await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<TId>(e, "Id")!.Equals(id));
    }

    /// <summary>
    /// Retrieve all entities of this type (base implementation).
    /// 
    /// PERFORMANCE OPTIMIZATION:
    /// - Uses AsNoTracking() to reduce memory consumption
    /// - Does not eager-load related entities (override if needed)
    /// 
    /// CAUTION: May return large result sets.
    /// Override this method in derived repositories to add:
    /// - Pagination support
    /// - Default filtering (e.g., non-deleted records)
    /// - Ordering logic
    /// </summary>
    /// <returns>Collection of all entities</returns>
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await DbSet
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Add a new entity to the repository (base implementation).
    /// 
    /// Persists entity to database with SaveChangesAsync.
    /// Primary key is auto-generated (BIGSERIAL) or set by entity constructor (UUID).
    /// 
    /// Business rule validation should be performed in:
    /// - Entity constructor (invariants)
    /// - Domain services (complex rules)
    /// - Application layer (use case validation)
    /// </summary>
    /// <param name="entity">Entity to add</param>
    /// <exception cref="ArgumentNullException">Thrown if entity is null</exception>
    public virtual async Task AddAsync(TEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        await DbSet.AddAsync(entity);
        await Context.SaveChangesAsync();
    }

    /// <summary>
    /// Update an existing entity (base implementation).
    /// 
    /// EF Core change tracking detects modified properties.
    /// Only changed fields are updated in SQL UPDATE statement.
    /// 
    /// For explicit property updates without full entity load:
    /// <code>
    /// Context.Entry(entity).Property(e => e.SomeProperty).IsModified = true;
    /// await Context.SaveChangesAsync();
    /// </code>
    /// </summary>
    /// <param name="entity">Entity with updated data</param>
    /// <exception cref="ArgumentNullException">Thrown if entity is null</exception>
    public virtual async Task UpdateAsync(TEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        DbSet.Update(entity);
        await Context.SaveChangesAsync();
    }

    /// <summary>
    /// Delete an entity by identifier (base implementation).
    /// 
    /// Foreign key constraints may prevent deletion:
    /// - CASCADE: Referenced entities are also deleted
    /// - RESTRICT: Throws exception if entity is referenced
    /// 
    /// Alternative Patterns:
    /// - Soft Delete: Add IsDeleted flag and filter queries
    /// - Archive: Move to archive table before deletion
    /// 
    /// Override this method in derived repositories to:
    /// - Implement soft delete logic
    /// - Add cascade delete handling
    /// - Validate delete permissions
    /// </summary>
    /// <param name="id">Entity identifier</param>
    /// <exception cref="InvalidOperationException">Thrown if entity does not exist</exception>
    public virtual async Task DeleteAsync(TId id)
    {
        var entity = await DbSet.FindAsync(id);
        
        if (entity == null)
            throw new InvalidOperationException($"Entity with ID {id} not found.");

        DbSet.Remove(entity);
        await Context.SaveChangesAsync();
    }
}
