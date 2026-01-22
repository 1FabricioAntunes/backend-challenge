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
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
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
    /// This is the PRIMARY SOURCE OF TRUTH for store balance. The balance is never
    /// persisted as a cached value; it is always calculated on-demand from transaction data.
    /// This ensures that the balance is always accurate and consistent with stored transactions.
    /// 
    /// Balance Calculation Algorithm:
    /// 1. Initialize total = 0 BRL
    /// 2. For each transaction in the collection:
    ///    a. Verify TransactionType navigation property is loaded (database lookup)
    ///    b. Convert amount from cents to BRL: Amount / 100.00
    ///    c. Multiply by type's sign from database: Amount/100 × TransactionType.Sign
    ///       - Credit types (4,5,6,7,8,9): Sign='+' → multiplier +1 (increases balance)
    ///       - Debit types (1,2,3): Sign='-' → multiplier -1 (decreases balance)
    ///    d. Add signed amount to total: total += signedAmount
    /// 3. Return final total balance in BRL
    /// 
    /// Balance Formula:
    /// Balance = Σ(Transaction.GetSignedAmount(TransactionType)) for all transactions
    /// 
    /// CNAB Transaction Type Classifications:
    /// 
    /// INFLOW TYPES (increase balance, Sign='+'):
    /// - Type 4: Crédito (Credit) - Direct credit deposits
    /// - Type 5: Recebimento Empr. (Business receipts) - Service payments
    /// - Type 6: Vendas (Sales receipts) - Customer payments for goods
    /// - Type 7: Recebimento TED (Electronic transfers) - Account-to-account
    /// - Type 8: Recebimento DOC (Bank transfers) - COMPE clearing house
    /// - Type 9: Transferência (Other credits) - Miscellaneous inflows
    /// 
    /// OUTFLOW TYPES (decrease balance, Sign='-'):
    /// - Type 1: Débito (Direct debit) - Account withdrawals
    /// - Type 2: Boleto (Boleto payments) - Slip-based debits
    /// - Type 3: Financiamento (Financing) - Loan/financing charges
    /// 
    /// Use Cases for this method:
    /// 
    /// 1. VERIFICATION: After file processing, verify calculated balance matches
    ///    expected amount to detect data integrity issues.
    /// 
    /// 2. RECONCILIATION: Audit stored transactions against independent balance
    ///    calculation to ensure no data corruption.
    /// 
    /// 3. REPORTING: Generate accurate balance reports by recalculating from
    ///    transaction data (source of truth).
    /// 
    /// 4. SNAPSHOT: Create transaction-based balance snapshot at any point in time
    ///    by filtering transactions by date and recalculating.
    /// 
    /// 5. VALIDATION: Detect duplicate or missing transactions by comparing
    ///    expected balance with calculated balance.
    /// 
    /// CRITICAL DATA REQUIREMENTS:
    /// - All transactions MUST have TransactionType navigation property loaded
    ///   from the database (eager-loaded via Include or similar)
    /// - If any transaction.TransactionType is null, an exception is thrown
    ///   This prevents silent bugs from incomplete data loads
    /// - The TransactionType.Sign value comes from the database lookup table,
    ///   not hardcoded, ensuring resilience to specification changes
    /// 
    /// Reference: docs/business-rules.md § Balance Calculation Rules
    /// Reference: docs/business-rules.md § CNAB Transaction Types table
    /// Reference: docs/database.md § Transaction Type Lookup Table
    /// Reference: CNAB 240 specification (80-character fixed-width format)
    /// </summary>
    /// <param name="transactions">List of transactions to calculate balance from.
    /// Each transaction's TransactionType navigation property MUST be loaded from database.
    /// Collection can be empty (returns 0 BRL), but cannot be null.</param>
    /// <returns>Calculated balance in BRL (decimal with 2 decimal places).
    /// Positive values indicate net inflow, negative values indicate net outflow.</returns>
    /// <remarks>
    /// Detailed Calculation Examples:
    /// 
    /// Example 1: Single credit transaction (Type 4 = Crédito)
    ///   Transaction: Amount=10000 (100.00 BRL), Type=4, Sign='+'
    ///   Calculation: 100.00 × (+1) = +100.00 BRL
    ///   Store Balance: 0 + 100.00 = 100.00 BRL
    /// 
    /// Example 2: Mixed credit and debit transactions
    ///   Transaction 1: Amount=50000 (500 BRL), Type=4 (Credit, Sign='+')
    ///     Signed amount: 500 × (+1) = +500 BRL
    ///   Transaction 2: Amount=20000 (200 BRL), Type=1 (Debit, Sign='-')
    ///     Signed amount: 200 × (-1) = -200 BRL
    ///   Transaction 3: Amount=15000 (150 BRL), Type=2 (Boleto, Sign='-')
    ///     Signed amount: 150 × (-1) = -150 BRL
    ///   Total Balance: 500 + (-200) + (-150) = 150.00 BRL
    /// 
    /// Example 3: Complex transaction set (realistic scenario)
    ///   Transaction 1: Type 6 (Sales), Amount 100000 (1000 BRL) → +1000.00
    ///   Transaction 2: Type 7 (TED receipt), Amount 300000 (3000 BRL) → +3000.00
    ///   Transaction 3: Type 1 (Debit), Amount 150000 (1500 BRL) → -1500.00
    ///   Transaction 4: Type 2 (Boleto), Amount 80000 (800 BRL) → -800.00
    ///   Transaction 5: Type 5 (Business receipt), Amount 500000 (5000 BRL) → +5000.00
    ///   Store Balance = 1000 + 3000 - 1500 - 800 + 5000 = 6700.00 BRL
    /// 
    /// Error Handling Examples:
    ///   - If transaction.TransactionType is null:
    ///     Throws InvalidOperationException with clear message about missing navigation property
    ///   - If transaction.TransactionType.Sign is invalid:
    ///     Transaction.GetSignedAmount() throws with details about type code and transaction ID
    /// 
    /// Performance Considerations:
    /// - O(n) algorithm: iterates once through all transactions
    /// - Requires TransactionType to be pre-loaded (use Include() in repository query)
    /// - No database round-trips during calculation
    /// - Safe for real-time balance queries
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if any transaction's TransactionType navigation property is not loaded from database</exception>
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
