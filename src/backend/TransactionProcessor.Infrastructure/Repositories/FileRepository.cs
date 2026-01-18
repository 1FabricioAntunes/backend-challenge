using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Domain.ValueObjects;
using TransactionProcessor.Infrastructure.Persistence;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for File aggregate using Entity Framework Core
/// Implements IFileRepository following Repository pattern.
/// 
/// EF Core Optimization Patterns:
/// - AsNoTracking() for all read-only queries (reduces memory usage by 30-40%)
/// - Include() for related entities to prevent N+1 queries
/// - Strategic eager loading only when needed
/// Optimized for normalized schema with statusCode FK
/// Uses FileStatusCode constants for resilient status filtering.
/// 
/// Reference: technical-decisions.md § 7 (EF Core Optimizations)
/// Reference: docs/database.md § EF Core Optimization Patterns
/// </summary>
public class FileRepository : IFileRepository
{
    private readonly ApplicationDbContext _context;

    public FileRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get file by unique identifier with related transactions and store navigation.
    /// 
    /// Eager-loads:
    /// - Transactions collection
    /// - Store navigation for each transaction
    /// - TransactionType navigation for signed amount calculation
    /// 
    /// This prevents N+1 queries when accessing transaction details and calculating balances.
    /// Use this method when you need full file details with transaction data.
    /// </summary>
    /// <param name="id">File identifier</param>
    /// <returns>File entity or null if not found</returns>
    public async Task<FileEntity?> GetByIdAsync(Guid id)
    {
        return await _context.Files
            .Include(f => f.Transactions)
                .ThenInclude(t => t.Store)
            .Include(f => f.Transactions)
                .ThenInclude(t => t.TransactionType)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    /// <summary>
    /// Get all files without related entities (read-only, optimized).
    /// 
    /// PERFORMANCE OPTIMIZATION:
    /// - Uses AsNoTracking() to reduce memory consumption
    /// - Does NOT load Transactions collection (use GetByIdAsync for details)
    /// - Suitable for list views, summaries, and reporting
    /// 
    /// CAUTION: May return large result sets; consider using GetQueryable()
    /// with pagination or filtering for production queries.
    /// </summary>
    /// <returns>Collection of all files</returns>
    public async Task<IEnumerable<FileEntity>> GetAllAsync()
    {
        return await _context.Files
            .AsNoTracking()
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get files with status = Uploaded (pending processing).
    /// 
    /// PERFORMANCE OPTIMIZATION:
    /// - Uses AsNoTracking() for read-only query
    /// - Does NOT load Transactions (files have no transactions yet)
    /// - Ordered by UploadedAt for FIFO processing
    /// 
    /// Used by SQS worker to find files ready for processing.
    /// Uses FileStatusCode constants for resilient status filtering.
    /// </summary>
    /// <returns>Collection of uploaded files awaiting processing</returns>
    public async Task<IEnumerable<FileEntity>> GetPendingFilesAsync()
    {
        return await _context.Files
            .AsNoTracking()
            .Where(f => f.StatusCode == FileStatusCode.Uploaded)
            .OrderBy(f => f.UploadedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Add new file to repository.
    /// 
    /// Business rules enforced:
    /// - File entity must be valid (constructor validates FileName)
    /// - Initial status is always Uploaded
    /// - File.Id must be unique (database enforces PK constraint)
    /// 
    /// Typically called after successful S3 upload.
    /// </summary>
    /// <param name="file">File entity to add</param>
    /// <exception cref="ArgumentNullException">Thrown if file is null</exception>
    public async Task AddAsync(FileEntity file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        await _context.Files.AddAsync(file);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update existing file (typically for status transitions).
    /// 
    /// Common update scenarios:
    /// - Uploaded → Processing (worker starts processing)
    /// - Processing → Processed (all transactions persisted successfully)
    /// - Processing → Rejected (validation or processing error)
    /// 
    /// EF Core will track changes to the entity and update only modified fields.
    /// </summary>
    /// <param name="file">File entity with updated data</param>
    /// <exception cref="ArgumentNullException">Thrown if file is null</exception>
    public async Task UpdateAsync(FileEntity file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        _context.Files.Update(file);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get queryable for advanced queries with projections.
    /// 
    /// Allows callers to:
    /// - Apply AsNoTracking() for read-only optimization
    /// - Apply custom filters (by status, date range, user)
    /// - Project to DTOs to minimize data transfer
    /// - Implement pagination for large result sets
    /// 
    /// Usage example:
    /// <code>
    /// var query = repository.GetQueryable()
    ///     .AsNoTracking()
    ///     .Where(f => f.StatusCode == FileStatusCode.Processed)
    ///     .Select(f => new FileSummaryDto 
    ///     { 
    ///         Id = f.Id, 
    ///         FileName = f.FileName,
    ///         ProcessedAt = f.ProcessedAt 
    ///     });
    /// </code>
    /// 
    /// Reference: docs/database.md § EF Core Optimization Patterns
    /// </summary>
    /// <returns>IQueryable for File entities</returns>
    public IQueryable<FileEntity> GetQueryable()
    {
        // Do not include related entities by default
        // Caller can add Include() as needed for specific queries
        return _context.Files;
    }
}
