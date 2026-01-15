using MediatR;
using TransactionProcessor.Application.DTOs;

namespace TransactionProcessor.Application.Queries.Files;

/// <summary>
/// Query to retrieve a single file by its unique identifier
/// </summary>
public class GetFileByIdQuery : IRequest<FileDto?>
{
    /// <summary>
    /// Unique identifier of the file to retrieve
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Constructor for query with file ID
    /// </summary>
    /// <param name="id">File identifier (must not be empty)</param>
    public GetFileByIdQuery(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("File ID cannot be empty.", nameof(id));
        }

        Id = id;
    }
}
