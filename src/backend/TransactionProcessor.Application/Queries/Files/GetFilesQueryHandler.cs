using MediatR;
using Microsoft.EntityFrameworkCore;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.Repositories;

namespace TransactionProcessor.Application.Queries.Files;

/// <summary>
/// Handler for GetFilesQuery that retrieves paginated file list
/// Uses AsNoTracking and projections for optimal read performance
/// </summary>
public class GetFilesQueryHandler : IRequestHandler<GetFilesQuery, PagedResult<FileDto>>
{
    private readonly IFileRepository _fileRepository;

    public GetFilesQueryHandler(IFileRepository fileRepository)
    {
        _fileRepository = fileRepository ?? throw new ArgumentNullException(nameof(fileRepository));
    }

    /// <summary>
    /// Handles the query and returns paginated file results
    /// </summary>
    /// <param name="request">Query with pagination parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated result with file DTOs</returns>
    public async Task<PagedResult<FileDto>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
    {
        // Validate pagination parameters
        if (request.Page < 1)
        {
            throw new ArgumentException("Page number must be greater than or equal to 1.", nameof(request.Page));
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            throw new ArgumentException("Page size must be between 1 and 100.", nameof(request.PageSize));
        }

        // Get queryable from repository
        var query = _fileRepository.GetQueryable();

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply AsNoTracking for read-only query (30-40% memory reduction)
        // Use Select projection to fetch only needed columns
        var items = await query
            .AsNoTracking()
            .OrderByDescending(f => f.UploadedAt) // Most recent files first
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(f => new FileDto
            {
                Id = f.Id,
                FileName = f.FileName,
                Status = f.Status.ToString(),
                UploadedAt = f.UploadedAt,
                ProcessedAt = f.ProcessedAt,
                TransactionCount = f.Transactions.Count(),
                ErrorMessage = f.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<FileDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }
}
