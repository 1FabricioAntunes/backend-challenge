using MediatR;
using TransactionProcessor.Application.DTOs;

namespace TransactionProcessor.Application.Queries.Files;

/// <summary>
/// Query to retrieve paginated list of files with their processing status
/// </summary>
public class GetFilesQuery : IRequest<PagedResult<FileDto>>
{
    /// <summary>
    /// Page number (1-based indexing)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page (1-100)
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Constructor for query with pagination parameters
    /// </summary>
    /// <param name="page">Page number (must be >= 1)</param>
    /// <param name="pageSize">Items per page (must be between 1 and 100)</param>
    public GetFilesQuery(int page = 1, int pageSize = 10)
    {
        Page = page;
        PageSize = pageSize;
    }
}
