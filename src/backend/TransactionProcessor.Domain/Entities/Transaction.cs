using TransactionProcessor.Domain.ValueObjects;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents a single CNAB transaction from a file.
/// Belongs to File aggregate and references a Store.
/// Immutable once created; transactions are never updated.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Unique identifier for the transaction (BIGSERIAL).
    /// Uses BIGSERIAL (not UUID) for optimized B-tree on high-write workloads.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// File this transaction belongs to (FK to Files table).
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Store that this transaction affects (FK to Stores table).
    /// </summary>
    public Guid StoreId { get; set; }

    /// <summary>
    /// Transaction type code (FK to transaction_types lookup table).
    /// Values 1-9 from CNAB specification.
    /// Determines whether transaction is credit or debit.
    /// </summary>
    public string TransactionTypeCode { get; set; } = string.Empty;

    /// <summary>
    /// Transaction amount in cents (divide by 100 for actual BRL value).
    /// Always stored as positive; sign determined by transaction type.
    /// Precision: 18,2 (supports up to 9,999,999.99 BRL).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Date when transaction occurred (DATE type, no time).
    /// Extracted from CNAB date field.
    /// </summary>
    public DateOnly TransactionDate { get; set; }

    /// <summary>
    /// Time when transaction occurred (TIME type).
    /// Extracted from CNAB time field.
    /// </summary>
    public TimeOnly TransactionTime { get; set; }

    /// <summary>
    /// Recipient CPF from CNAB (11 digits, may include formatting).
    /// </summary>
    public string CPF { get; set; } = string.Empty;

    /// <summary>
    /// Card used in transaction from CNAB (12 characters).
    /// </summary>
    public string Card { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when transaction was persisted (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when transaction was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// The file this transaction came from (navigational property).
    /// </summary>
    public FileEntity? File { get; set; }

    /// <summary>
    /// The store this transaction affects (navigational property).
    /// </summary>
    public Store? Store { get; set; }

    /// <summary>
    /// The transaction type lookup entity (FK to transaction_types table).
    /// Navigational property for querying type details and sign information.
    /// Must be eager-loaded when calculating signed amounts.
    /// </summary>
    public TransactionType? TransactionType { get; set; }

    /// <summary>
    /// Calculates the signed amount based on transaction type from database lookup.
    /// 
    /// NOTE: Sign is retrieved from the transaction_types lookup table, not hardcoded.
    /// This makes the code resilient to database changes - if the sign of a type changes,
    /// the code automatically adapts without recompilation.
    /// </summary>
    /// <param name="transactionType">TransactionType entity with sign from database</param>
    /// <returns>Signed amount in BRL (Amount/100 with appropriate sign from database)</returns>
    /// <remarks>
    /// Example: Amount=10000, TransactionType.Sign="-" → GetSignedAmount(type)=-100 BRL (debit)
    ///         Amount=10000, TransactionType.Sign="+" → GetSignedAmount(type)=+100 BRL (credit)
    /// 
    /// The sign value comes from the database transaction_types.sign column, not hardcoded logic.
    /// </remarks>
    public decimal GetSignedAmount(TransactionType transactionType)
    {
        if (transactionType == null)
            throw new ArgumentNullException(nameof(transactionType));

        var signMultiplier = transactionType.GetSignMultiplier();
        return signMultiplier * (Amount / 100m);
    }

    /// <summary>
    /// Legacy method - DEPRECATED. Do NOT use. Kept for compilation compatibility only.
    /// 
    /// This method used hardcoded logic which breaks if database changes.
    /// Always use GetSignedAmount(TransactionType) instead.
    /// </summary>
    [Obsolete("Hardcoded sign logic is unreliable. Use GetSignedAmount(TransactionType) to ensure database-driven sign values are used", error: true)]
    public decimal GetSignedAmount()
    {
        throw new NotSupportedException(
            "GetSignedAmount() without TransactionType parameter is not supported. " +
            "Hardcoded logic cannot adapt if database changes. " +
            "Use GetSignedAmount(TransactionType) with entity from database.");
    }
}
