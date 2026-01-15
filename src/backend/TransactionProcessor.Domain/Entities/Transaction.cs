using TransactionProcessor.Domain.ValueObjects;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents a single CNAB transaction belonging to a file.
/// </summary>
public class Transaction
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Description { get; set; } = string.Empty;
}
