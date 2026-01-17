namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents a store that receives transactions.
/// Aggregate root for store-related transactions.
/// Balance is computed on-demand from transactions; not persisted.
/// </summary>
public class Store
{
    /// <summary>
    /// Unique identifier for the store.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Store name from CNAB file (max 19 characters).
    /// Part of composite unique key with OwnerName.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Store owner name from CNAB file (max 14 characters).
    /// Part of composite unique key with Name.
    /// </summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when store was created or first appeared in a file.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when store was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// Collection of all transactions for this store.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
