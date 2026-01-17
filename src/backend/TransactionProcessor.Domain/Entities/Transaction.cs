using TransactionProcessor.Domain.ValueObjects;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents a single CNAB transaction from a file.
/// Belongs to File aggregate and references a Store.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Unique identifier for the transaction.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// File this transaction belongs to.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Store that this transaction affects.
    /// </summary>
    public Guid StoreId { get; set; }

    /// <summary>
    /// Transaction type (1-9) from CNAB.
    /// Determines if credit or debit.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Transaction amount in cents (divided by 100 for actual value).
    /// Always stored as positive; sign determined by Type.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date when transaction occurred (from CNAB file).
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Time when transaction occurred (from CNAB file).
    /// </summary>
    public TimeSpan OccurredAtTime { get; set; }

    /// <summary>
    /// Recipient CPF from CNAB (11 digits, may include formatting).
    /// </summary>
    public string CPF { get; set; } = string.Empty;

    /// <summary>
    /// Card used in transaction from CNAB (12 characters).
    /// </summary>
    public string Card { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when transaction was persisted.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// The file this transaction came from.
    /// </summary>
    public FileEntity? File { get; set; }

    /// <summary>
    /// The store this transaction affects.
    /// </summary>
    public Store? Store { get; set; }

    /// <summary>
    /// Calculates the signed amount based on transaction type.
    /// Positive for inflow (types 1,4,5,6,7,8), negative for outflow (types 2,3,9).
    /// </summary>
    /// <returns>Signed amount (Amount/100 with appropriate sign)</returns>
    public decimal GetSignedAmount()
    {
        // Inflow types (1,4,5,6,7,8): positive
        // Outflow types (2,3,9): negative
        if (Type is 1 or 4 or 5 or 6 or 7 or 8)
            return Amount / 100m;
        if (Type is 2 or 3 or 9)
            return -(Amount / 100m);
        
        throw new InvalidOperationException($"Invalid transaction type: {Type}");
    }
}
