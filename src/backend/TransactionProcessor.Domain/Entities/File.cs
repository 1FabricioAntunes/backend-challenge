using TransactionProcessor.Domain.ValueObjects;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents an uploaded file and its processing state.
/// </summary>
public class File
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public FileStatus Status { get; set; } = FileStatus.Uploaded;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
