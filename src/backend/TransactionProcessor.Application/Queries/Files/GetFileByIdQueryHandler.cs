using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.Repositories;

namespace TransactionProcessor.Application.Queries.Files;

/// <summary>
/// Handler for GetFileByIdQuery that retrieves a single file by ID
/// Uses AsNoTracking and projections for optimal read performance
/// </summary>
public class GetFileByIdQueryHandler : IRequestHandler<GetFileByIdQuery, FileDto?>
{
    private readonly IFileRepository _fileRepository;

    public GetFileByIdQueryHandler(IFileRepository fileRepository)
    {
        _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
    }

    /// <summary>
    /// Handles the query and returns file DTO or null if not found
    /// </summary>
    /// <param name="request">Query with file ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>FileDto if found, null otherwise</returns>
    public async Task<FileDto?> Handle(GetFileByIdQuery request, CancellationToken cancellationToken)
    {
        // Get queryable from repository
        var query = _fileRepository.GetQueryable();

        // Apply AsNoTracking for read-only query
        // Use Select projection to fetch only needed data
        var fileDto = await query
            .AsNoTracking()
            .Where(f => f.Id == request.Id)
            .Select(f => new FileDto
            {
                Id = f.Id,
                FileName = f.FileName,
                Status = f.StatusCode,
                UploadedAt = f.UploadedAt,
                ProcessedAt = f.ProcessedAt,
                TransactionCount = f.Transactions.Count(),
                ErrorMessage = f.ErrorMessage
            })
            .FirstOrDefaultAsync(cancellationToken);

        return fileDto;
    }
}
