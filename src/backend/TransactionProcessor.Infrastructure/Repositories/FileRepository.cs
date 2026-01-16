using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Persistence;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for File aggregate using Entity Framework Core
/// Implements IFileRepository following Repository pattern
/// </summary>
public class FileRepository : IFileRepository
{
    private readonly ApplicationDbContext _context;

    public FileRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get file by unique identifier with related transactions
    /// </summary>
    /// <param name="id">File identifier</param>
    /// <returns>File entity or null if not found</returns>
    public async Task<FileEntity?> GetByIdAsync(Guid id)
    {
        return await _context.Files
            .Include(f => f.Transactions)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    /// <summary>
    /// Get all files (use with caution, prefer GetQueryable for large datasets)
    /// </summary>
    /// <returns>Collection of all files</returns>
    public async Task<IEnumerable<FileEntity>> GetAllAsync()
    {
        return await _context.Files
            .Include(f => f.Transactions)
            .ToListAsync();
    }

    /// <summary>
    /// Get files with status = Uploaded (pending processing)
    /// </summary>
    /// <returns>Collection of uploaded files awaiting processing</returns>
    public async Task<IEnumerable<FileEntity>> GetPendingFilesAsync()
    {
        return await _context.Files
            .Where(f => f.Status == Domain.ValueObjects.FileStatus.Uploaded)
            .OrderBy(f => f.UploadedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Add new file to repository
    /// </summary>
    /// <param name="file">File entity to add</param>
    public async Task AddAsync(FileEntity file)
    {
        await _context.Files.AddAsync(file);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Update existing file
    /// </summary>
    /// <param name="file">File entity with updated data</param>
    public async Task UpdateAsync(FileEntity file)
    {
        _context.Files.Update(file);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get queryable for advanced queries with projections
    /// Allows callers to apply AsNoTracking and custom projections
    /// </summary>
    /// <returns>IQueryable for File entities</returns>
    public IQueryable<FileEntity> GetQueryable()
    {
        return _context.Files.Include(f => f.Transactions);
    }
}
