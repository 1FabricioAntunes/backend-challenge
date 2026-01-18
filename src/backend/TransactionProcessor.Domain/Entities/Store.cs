namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents a store (receiving entity) for CNAB transactions.
/// Aggregate root for store-related business operations.
/// 
/// Invariants:
/// - Id is immutable (set only in constructor)
/// - OwnerName and Name are required and together form a composite unique identifier
/// - Balance is computed from transactions using signed amounts (not persisted as direct value)
/// - Balance cannot be negative
/// 
/// The Store tracks all transactions and can calculate its current balance
/// by summing the signed amounts of all associated transactions.
/// </summary>
public class Store
{
    /// <summary>
    /// Unique identifier for the store (UUID v7, time-ordered).
    /// Immutable; set only in constructor to enforce aggregate identity.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Store owner/recipient name from CNAB file (max 14 characters).
    /// Part of composite unique key with Name for idempotent upserts.
    /// Required; cannot be null or empty.
    /// </summary>
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>
    /// Store name from CNAB file (max 19 characters).
    /// Part of composite unique key with OwnerName.
    /// Required; cannot be null or empty.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current store balance in BRL (decimal).
    /// Calculated from transactions using signed amounts.
    /// Never persisted directly; always recomputed from transaction sums.
    /// 
    /// Balance = Sum of (Transaction.Amount / 100.00 * Transaction.Type.Sign)
    /// 
    /// Where Transaction.Type.Sign is:
    /// - Credit types (2, 3, 9): +1 (increases balance)
    /// - Debit types (1, 4, 5, 6, 7, 8): -1 (decreases balance)
    /// </summary>
    public decimal Balance { get; private set; }

    /// <summary>
    /// Timestamp when store was created or first appeared in a file (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when store was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// Collection of all transactions associated with this store.
    /// Used to calculate balance and track all activity.
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Constructor for creating a new Store aggregate.
    /// 
    /// Validates invariants:
    /// - OwnerName is required
    /// - Name is required
    /// - Id is set to provided value and becomes immutable
    /// </summary>
    /// <param name="id">Unique identifier (UUID v7)</param>
    /// <param name="ownerName">Owner/recipient name (required, max 14 chars)</param>
    /// <param name="name">Store name (required, max 19 chars)</param>
    /// <exception cref="ArgumentException">Thrown if ownerName or name are null or empty</exception>
    public Store(Guid id, string ownerName, string name)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
            throw new ArgumentException("Owner name is required.", nameof(ownerName));
        
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Store name is required.", nameof(name));

        Id = id;
        OwnerName = ownerName;
        Name = name;
        Balance = 0m;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Constructor for EF Core mapping (parameterless).
    /// Used by the ORM; business logic should use the explicit constructor above.
    /// </summary>
    public Store()
    {
    }

    /// <summary>
    /// Updates the store balance with validation.
    /// 
    /// Business rule: Store balance cannot be negative.
    /// This method is called after transaction persistence to reflect new state.
    /// </summary>
    /// <param name="newBalance">New balance value in BRL</param>
    /// <exception cref="ArgumentException">Thrown if newBalance is negative</exception>
    public void UpdateBalance(decimal newBalance)
    {
        if (newBalance < 0)
            throw new ArgumentException("Balance cannot be negative.", nameof(newBalance));

        Balance = newBalance;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates the store balance from all associated transactions.
    /// 
    /// Algorithm:
    /// 1. For each transaction in the collection:
    ///    - Divide Amount by 100.00 to convert from cents to BRL
    ///    - Multiply by the transaction type's Sign multiplier (from database lookup)
    ///      * Credit types (2, 3, 9): Sign = "+" → multiplier +1 (increases balance)
    ///      * Debit types (1, 4, 5, 6, 7, 8): Sign = "-" → multiplier -1 (decreases balance)
    /// 2. Sum all signed amounts
    /// 3. Return total balance
    /// 
    /// This method is used to:
    /// - Verify balance consistency during file processing
    /// - Recompute balance if needed for audits
    /// - Serve as source of truth when persistent balance cache is unavailable
    /// - Verify data integrity after transaction persistence
    /// 
    /// Reference: docs/business-rules.md § Transaction Types table
    /// for sign mappings (CNAB specification).
    /// </summary>
    /// <param name="transactions">List of transactions to calculate balance from, with TransactionType navigation property loaded</param>
    /// <returns>Calculated balance in BRL</returns>
    /// <remarks>
    /// Example calculations:
    /// - Transaction 1: Amount=10000 (100 BRL), Type 1 (Debit, Sign="-") → -100 BRL
    /// - Transaction 2: Amount=50000 (500 BRL), Type 4 (Credit, Sign="+") → +500 BRL
    /// - Result: -100 + 500 = 400 BRL balance
    /// 
    /// IMPORTANT: This method requires that transactions have their TransactionType
    /// navigation property loaded. If TransactionType is null, an exception is thrown.
    /// This ensures data integrity and prevents silent bugs from incomplete data loads.
    /// 
    /// Note: The sign comes from the database transaction_types lookup table,
    /// not hardcoded. This makes the calculation resilient to specification changes.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if any transaction's TransactionType is not loaded</exception>
    public decimal CalculateBalance(List<Transaction> transactions)
    {
        if (transactions == null || transactions.Count == 0)
            return 0m;

        decimal total = 0m;

        foreach (var transaction in transactions)
        {
            // Require TransactionType to be loaded (eager loaded from database)
            if (transaction.TransactionType == null)
                throw new InvalidOperationException(
                    $"Transaction {transaction.Id} missing TransactionType navigation property. " +
                    "Ensure TransactionType is eager-loaded before calling CalculateBalance.");

            // Use the GetSignedAmount method which handles the sign calculation
            var signedAmount = transaction.GetSignedAmount(transaction.TransactionType);
            total += signedAmount;
        }

        return total;
    }
}
