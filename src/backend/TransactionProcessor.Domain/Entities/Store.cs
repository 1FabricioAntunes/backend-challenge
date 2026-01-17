namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents a store that receives transactions.
/// Aggregate root for store-related transactions.
/// </summary>
public class Store
{
    /// <summary>
    /// Unique identifier for the store.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Store code from CNAB file (unique, max 14 characters).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Store name from CNAB file (max 19 characters).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current account balance calculated from all transactions.
    /// Updated during CNAB file processing.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Timestamp when store was created or first appeared in a file.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when store balance was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// Collection of all transactions for this store.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
