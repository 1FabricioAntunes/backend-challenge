using TransactionProcessor.Domain.Entities;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Domain.Repositories;

/// <summary>
/// Repository interface for File aggregate operations
/// Follows Repository pattern with async operations
/// </summary>
public interface IFileRepository
{
    /// <summary>
    /// Get file by unique identifier with related entities
    /// </summary>
    /// <param name="id">File identifier</param>
    /// <returns>File entity or null if not found</returns>
    Task<FileEntity?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get all files (use with caution, consider pagination)
    /// </summary>
    /// <returns>Collection of all files</returns>
    Task<IEnumerable<FileEntity>> GetAllAsync();

    /// <summary>
    /// Get files with status = Uploaded (pending processing)
    /// </summary>
    /// <returns>Collection of uploaded files awaiting processing</returns>
    Task<IEnumerable<FileEntity>> GetPendingFilesAsync();

    /// <summary>
    /// Add new file to repository
    /// </summary>
    /// <param name="file">File entity to add</param>
    Task AddAsync(FileEntity file);

    /// <summary>
    /// Update existing file
    /// </summary>
    /// <param name="file">File entity with updated data</param>
    Task UpdateAsync(FileEntity file);

    /// <summary>
    /// Get queryable for advanced queries with projections
    /// Allows callers to apply AsNoTracking and custom projections
    /// </summary>
    /// <returns>IQueryable for File entities</returns>
    IQueryable<FileEntity> GetQueryable();
}
