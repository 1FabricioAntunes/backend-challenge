using TransactionProcessor.Domain.Entities;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Domain.Repositories;

/// <summary>
/// Repository interface for File aggregate root operations.
/// 
/// Defines the contract for persisting and retrieving File aggregates.
/// Implementations should handle transactional consistency and enforce invariants.
/// 
/// Key responsibilities:
/// - CRUD operations for File aggregates
/// - Querying files by status for processing workflows
/// - Supporting transaction-aware persistence (all-or-nothing per file)
/// - Eager-loading related entities to prevent N+1 queries
/// 
/// Reference: Technical Decisions § 4.2 (Domain-Driven Design) and
/// docs/architecture.md § Infrastructure Layer for DDD repository patterns.
/// </summary>
public interface IFileRepository
{
    /// <summary>
    /// Retrieve file by unique identifier with all related entities.
    /// 
    /// Should eager-load the Transactions collection along with their
    /// related Store and TransactionType entities to support:
    /// - Balance calculations
    /// - Transaction detail inspection
    /// - All-or-nothing processing verification
    /// 
    /// Reference: docs/async-processing.md for processing requirements.
    /// </summary>
    /// <param name="id">File identifier (UUID v7)</param>
    /// <returns>File aggregate with transactions loaded, or null if not found</returns>
    Task<FileEntity?> GetByIdAsync(Guid id);

    /// <summary>
    /// Retrieve all files in the system.
    /// 
    /// CAUTION: May return large result sets; consider pagination or filtering
    /// for production queries. Typically used for admin/reporting purposes.
    /// 
    /// Should use AsNoTracking() for read-only performance optimization.
    /// May not need to load Transactions collection depending on use case.
    /// </summary>
    /// <returns>Collection of all File entities</returns>
    Task<IEnumerable<FileEntity>> GetAllAsync();

    /// <summary>
    /// Retrieve files pending processing (status = Uploaded).
    /// 
    /// Used by the SQS worker to find files ready for processing.
    /// Files remain in this state until worker picks them up.
    /// 
    /// Should use AsNoTracking() for performance optimization.
    /// May not need to load Transactions collection; just identify pending files.
    /// </summary>
    /// <returns>Collection of files with status = Uploaded, ready for processing</returns>
    Task<IEnumerable<FileEntity>> GetPendingFilesAsync();

    /// <summary>
    /// Add a new file to the repository.
    /// 
    /// Business rules:
    /// - File must have valid FileName (enforced by File constructor)
    /// - File.Id must be unique (immutable UUID v7)
    /// - File must have S3Key set before persistence
    /// - Initial status is always Uploaded
    /// 
    /// Typically called after successful S3 upload via IFileStorageService.
    /// 
    /// Implementation should:
    /// - Validate File invariants
    /// - Persist to database with status = Uploaded
    /// - Not persist transactions at this stage (added later during processing)
    /// </summary>
    /// <param name="file">New File aggregate to persist</param>
    /// <exception cref="ArgumentNullException">Thrown if file is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if file with same Id already exists</exception>
    Task AddAsync(FileEntity file);

    /// <summary>
    /// Update an existing file.
    /// 
    /// Business rules:
    /// - File.Id is immutable and cannot be changed
    /// - File.FileName is immutable and cannot be changed
    /// - File.UploadedAt is immutable and cannot be changed
    /// - StatusCode can only transition according to state machine
    /// - ProcessedAt and ErrorMessage are set by status transitions only
    /// 
    /// Typical update scenarios:
    /// 1. Uploaded → Processing (worker starts)
    /// 2. Processing → Processed (all transactions persisted successfully)
    /// 3. Processing → Rejected (validation or processing error)
    /// 
    /// Implementation should enforce transition rules to prevent invalid states.
    /// 
    /// Reference: docs/business-rules.md § File States for valid transitions.
    /// </summary>
    /// <param name="file">File aggregate with updated status and metadata</param>
    /// <exception cref="ArgumentNullException">Thrown if file is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if file does not exist or invalid status transition</exception>
    Task UpdateAsync(FileEntity file);

    /// <summary>
    /// Get queryable for advanced queries with projections and filtering.
    /// 
    /// Allows callers to:
    /// - Apply AsNoTracking() for read-only optimization
    /// - Apply custom filters (by status, date range, etc.)
    /// - Project to DTOs to minimize data transfer
    /// - Support complex queries without duplicating repository methods
    /// 
    /// Usage example:
    /// <code>
    /// var fileQuery = repository.GetQueryable()
    ///     .Where(f => f.StatusCode == FileStatusCode.Processed)
    ///     .AsNoTracking()
    ///     .Select(f => new FileSummaryDto { Id = f.Id, FileName = f.FileName });
    /// </code>
    /// 
    /// Reference: docs/database.md § EF Core Optimization Patterns
    /// for AsNoTracking and projection best practices.
    /// </summary>
    /// <returns>IQueryable for File entities, unfiltered and untracked</returns>
    IQueryable<FileEntity> GetQueryable();
}
