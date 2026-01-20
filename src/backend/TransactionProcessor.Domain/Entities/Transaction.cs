using TransactionProcessor.Domain.ValueObjects;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Represents a single CNAB transaction parsed from a file.
/// Entity within the File aggregate; immutable once created.
/// 
/// CNAB Transaction Properties (80-character fixed-width format):
/// - Type (1 digit): 1-9 indicating credit/debit
/// - Date (8 digits): YYYYMMDD of occurrence
/// - Amount (10 digits): Transaction amount in cents
/// - CPF (11 digits): Recipient CPF
/// - Card (12 characters): Card identifier
/// - Time (6 digits): HHMMSS of occurrence
/// - Store Owner (14 characters): Store representative name
/// - Store Name (19 characters): Store name
/// 
/// Key Features:
/// - Uses long (BIGSERIAL) for Id instead of Guid for INSERT performance on high-volume tables
/// - Amount stored in cents (divide by 100 for BRL)
/// - Sign determined by transaction type from database lookup
/// - Immutable after creation (no property setters)
/// - All validations performed in constructor
/// 
/// Reference: docs/business-rules.md § CNAB File Format (80-character lines)
/// Reference: docs/database.md § Primary Key Strategy (BIGSERIAL rationale)
/// </summary>
public class Transaction
{
    /// <summary>
    /// Unique identifier for the transaction.
    /// 
    /// Uses PostgreSQL BIGSERIAL (long) instead of UUID for performance optimization:
    /// - BIGSERIAL: Sequential insert-friendly, smaller index sizes, faster insertions
    /// - UUID: Random ordering causes B-tree fragmentation on high-write tables
    /// 
    /// This table expects high transaction volume during file processing (1000s per file).
    /// BIGSERIAL provides optimal performance for this use case.
    /// 
    /// Immutable; set only in constructor.
    /// </summary>
    public long Id { get; private set; }

    /// <summary>
    /// File this transaction belongs to (FK to Files table).
    /// Can be set by parent File aggregate during construction.
    /// </summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Store that this transaction affects (FK to Stores table).
    /// Immutable; set only in constructor.
    /// </summary>
    public Guid StoreId { get; private set; }

    /// <summary>
    /// Transaction type code (FK to transaction_types lookup table).
    /// Values: 1-9 from CNAB specification.
    /// 
    /// Type Mapping (from docs/business-rules.md):
    /// - Type 1: Débito (Debit, Sign = -1)
    /// - Type 2: Boleto (Outflow, Sign = -1)
    /// - Type 3: Financiamento (Outflow, Sign = -1)
    /// - Type 4: Crédito (Credit, Sign = +1)
    /// - Type 5: Recebimento Empr. (Inflow, Sign = +1)
    /// - Type 6: Vendas (Inflow, Sign = +1)
    /// - Type 7: Recebimento TED (Inflow, Sign = +1)
    /// - Type 8: Recebimento DOC (Inflow, Sign = +1)
    /// - Type 9: Aluguel (Outflow, Sign = -1)
    /// 
    /// Determined by transaction type from database lookup (TransactionType.Sign).
    /// Immutable; set only in constructor.
    /// </summary>
    public string TransactionTypeCode { get; private set; } = string.Empty;

    /// <summary>
    /// Transaction amount in cents (not BRL).
    /// 
    /// Example: 10000 cents = 100.00 BRL
    /// Always stored as positive value; sign determined by TransactionType.Sign.
    /// 
    /// Precision: decimal(18,2) in database supports up to 9,999,999.99 BRL.
    /// Stored as cents to avoid floating-point precision issues.
    /// 
    /// Business rule: Amount must be greater than 0 (enforced in constructor).
    /// Immutable; set only in constructor.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Date when transaction occurred (DATE type, no time component).
    /// Extracted from CNAB date field (positions 2-9, format YYYYMMDD).
    /// Immutable; set only in constructor.
    /// </summary>
    public DateOnly TransactionDate { get; private set; }

    /// <summary>
    /// Time when transaction occurred (TIME type).
    /// Extracted from CNAB time field (positions 43-48, format HHMMSS).
    /// Immutable; set only in constructor.
    /// </summary>
    public TimeOnly TransactionTime { get; private set; }

    /// <summary>
    /// Recipient CPF from CNAB (11 digits, positions 20-30).
    /// May include formatting characters; no validation applied.
    /// Immutable; set only in constructor.
    /// </summary>
    public string CPF { get; private set; } = string.Empty;

    /// <summary>
    /// Card used in transaction from CNAB (12 characters, positions 31-42).
    /// Card identifier or authorization code.
    /// Immutable; set only in constructor.
    /// </summary>
    public string Card { get; private set; } = string.Empty;

    /// <summary>
    /// Timestamp when transaction was persisted to database (UTC).
    /// Set during repository persistence, typically to current UTC time.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when transaction was last updated (UTC).
    /// Transactions are immutable in business logic; updates should be rare.
    /// Set during persistence operations.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    /// <summary>
    /// The file this transaction came from (navigational property).
    /// Loaded via File aggregate relationship.
    /// </summary>
    public FileEntity? File { get; set; }

    /// <summary>
    /// The store this transaction affects (navigational property).
    /// Loaded via Store aggregate relationship.
    /// </summary>
    public Store? Store { get; set; }

    /// <summary>
    /// The transaction type lookup entity (FK to transaction_types table).
    /// Navigational property for accessing type details and sign multiplier.
    /// Must be eager-loaded from database when calculating signed amounts.
    /// Cannot be null when calling GetSignedAmount().
    /// </summary>
    public TransactionType? TransactionType { get; set; }

    /// <summary>
    /// Constructor for creating a new Transaction with business rule validation.
    /// 
    /// Validates invariants:
    /// - TypeCode must be 1-9 (valid CNAB type)
    /// - Amount must be greater than 0
    /// - FileId and StoreId must be valid GUIDs
    /// - All other properties required (non-null/non-empty strings)
    /// 
    /// All properties become immutable after construction.
    /// </summary>
    /// <param name="fileId">Parent file identifier</param>
    /// <param name="storeId">Target store identifier</param>
    /// <param name="transactionTypeCode">Transaction type code (1-9)</param>
    /// <param name="amount">Transaction amount in cents (must be > 0)</param>
    /// <param name="transactionDate">Date of transaction occurrence</param>
    /// <param name="transactionTime">Time of transaction occurrence</param>
    /// <param name="cpf">Recipient CPF from CNAB</param>
    /// <param name="card">Card identifier from CNAB</param>
    /// <exception cref="ArgumentException">Thrown if typeCode not in 1-9 or amount <= 0</exception>
    public Transaction(
        Guid fileId,
        Guid storeId,
        string transactionTypeCode,
        decimal amount,
        DateOnly transactionDate,
        TimeOnly transactionTime,
        string cpf,
        string card)
    {
        // Validate TypeCode (1-9)
        if (!int.TryParse(transactionTypeCode, out int typeCodeInt) || typeCodeInt < 1 || typeCodeInt > 9)
            throw new ArgumentException(
                $"Transaction type code must be between 1 and 9. Received: {transactionTypeCode}",
                nameof(transactionTypeCode));

        // Validate Amount > 0
        if (amount <= 0)
            throw new ArgumentException(
                $"Transaction amount must be greater than 0. Received: {amount}",
                nameof(amount));

        // Validate transaction date is not in the future (business rule)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (transactionDate > today)
            throw new ArgumentException(
                $"Transaction date cannot be in the future. Received: {transactionDate}",
                nameof(transactionDate));

        // Set immutable properties
        FileId = fileId;
        StoreId = storeId;
        TransactionTypeCode = transactionTypeCode;
        Amount = amount;
        TransactionDate = transactionDate;
        TransactionTime = transactionTime;
        CPF = cpf ?? string.Empty;
        Card = card ?? string.Empty;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Constructor for EF Core mapping (parameterless).
    /// Used by the ORM for deserialization from database.
    /// Business logic should use the explicit constructor above.
    /// </summary>
    public Transaction()
    {
    }

    /// <summary>
    /// Calculates the signed amount based on transaction type from database.
    /// 
    /// This method implements the CNAB credit/debit calculation rule:
    /// Each transaction type has an associated sign (credit or debit) that determines
    /// whether the amount increases or decreases the store's balance.
    /// 
    /// Algorithm:
    /// 1. Convert amount from cents to BRL: Amount / 100.00
    /// 2. Retrieve sign multiplier from database: TransactionType.Sign
    /// 3. Calculate signed amount: Amount/100.00 × SignMultiplier
    /// 4. Return result (positive for income, negative for expense)
    /// 
    /// CNAB Credit/Debit Rules (80-character format, position 1 = type):
    /// 
    /// CREDIT TYPES (increase store balance):
    /// - Type 4: Crédito (Credit) - Direct credit to account
    /// - Type 5: Recebimento Empr. (Inflow) - Business receipt
    /// - Type 6: Vendas (Inflow) - Sales proceeds
    /// - Type 7: Recebimento TED (Inflow) - Electronic transfer receipt
    /// - Type 8: Recebimento DOC (Inflow) - Bank transfer receipt
    /// - Type 9: Transferência (Credit) - Transfer received
    /// Sign from database: '+' → multiplier +1.0m
    /// Result: Transaction amount added to balance
    /// 
    /// DEBIT TYPES (decrease store balance):
    /// - Type 1: Débito (Debit) - Direct debit from account
    /// - Type 2: Boleto (Outflow) - Boleto payment/debt
    /// - Type 3: Financiamento (Outflow) - Financing/loan charge
    /// Sign from database: '-' → multiplier -1.0m
    /// Result: Transaction amount subtracted from balance
    /// 
    /// CRITICAL DESIGN PRINCIPLE - Database-Driven Sign Calculation:
    /// The sign value is retrieved from the transaction_types lookup table in the database,
    /// NOT hardcoded in this method. This architecture choice ensures:
    /// 
    /// 1. RESILIENCE: If business rules change and type classifications shift,
    ///    only the database needs updating. No code recompilation required.
    /// 
    /// 2. CONSISTENCY: All parts of the system use the same sign value from
    ///    a single source of truth (the database lookup table).
    /// 
    /// 3. AUDITABILITY: Database queries can easily show historical changes
    ///    to type definitions, providing compliance trail.
    /// 
    /// 4. SEPARATION OF CONCERNS: Business rules (stored in database) are
    ///    separate from algorithm implementation (this code).
    /// 
    /// Reference: docs/business-rules.md § CNAB Transaction Types table
    /// Reference: docs/database.md § Transaction Type Lookup table design
    /// Reference: CNAB 240 specification (80-character fixed-width format)
    /// </summary>
    /// <param name="transactionType">TransactionType entity with sign retrieved from database lookup</param>
    /// <returns>Signed amount in BRL (positive adds to balance, negative subtracts)</returns>
    /// <remarks>
    /// Real-world examples with Type-to-Balance impact:
    /// 
    /// Example 1: Sale proceeds (Type 6 = Inflow)
    ///   Amount: 25000 (cents) = 250.00 BRL
    ///   Type: 6 (Vendas/Sales, Sign='+' from database)
    ///   Calculation: 250.00 × (+1) = +250.00 BRL
    ///   Impact: Store balance increases by 250 BRL
    /// 
    /// Example 2: Boleto payment (Type 2 = Outflow)
    ///   Amount: 15000 (cents) = 150.00 BRL
    ///   Type: 2 (Boleto, Sign='-' from database)
    ///   Calculation: 150.00 × (-1) = -150.00 BRL
    ///   Impact: Store balance decreases by 150 BRL
    /// 
    /// Example 3: Bank transfer (Type 7 = Inflow)
    ///   Amount: 500000 (cents) = 5000.00 BRL
    ///   Type: 7 (Recebimento TED, Sign='+' from database)
    ///   Calculation: 5000.00 × (+1) = +5000.00 BRL
    ///   Impact: Store balance increases by 5000 BRL
    /// 
    /// Usage: This method is called by Store.CalculateBalance(transactions)
    /// to compute the total store balance by summing signed amounts of all
    /// associated transactions.
    /// 
    /// Data Requirements:
    /// - TransactionType must be eagerly loaded from database (not lazy-loaded)
    /// - Ensures TransactionType.Sign contains the correct database value
    /// - Prevents N+1 queries during balance calculation
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if transactionType is null (missing database-loaded entity)</exception>
    /// <exception cref="InvalidOperationException">Thrown if TransactionType.Sign value is invalid (not '+' or '-')</exception>
    public decimal GetSignedAmount(TransactionType transactionType)
    {
        if (transactionType == null)
            throw new ArgumentNullException(
                nameof(transactionType),
                "TransactionType cannot be null. Ensure TransactionType is eager-loaded from database before calling GetSignedAmount.");

        try
        {
            var signMultiplier = transactionType.GetSignMultiplier();
            return signMultiplier * (Amount / 100m);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Failed to calculate signed amount for transaction {Id}. " +
                $"TransactionType code {TransactionTypeCode} has invalid sign value '{transactionType.Sign}'. " +
                $"Valid signs are '+' (credit) or '-' (debit).",
                ex);
        }
    }

    /// <summary>
    /// Legacy method - DEPRECATED. Do NOT use. Kept for compilation compatibility only.
    /// 
    /// This method used hardcoded logic which breaks if database changes.
    /// Always use GetSignedAmount(TransactionType) instead.
    /// 
    /// Hardcoded logic violates the principle that sign determination should
    /// come from the database lookup table, allowing resilient updates.
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
