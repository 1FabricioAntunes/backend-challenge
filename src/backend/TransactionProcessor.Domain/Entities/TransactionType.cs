namespace TransactionProcessor.Domain.Entities;

/// <summary>
/// Transaction type lookup entity representing rows from transaction_types table.
/// 
/// This table stores the definition of each CNAB transaction type (1-9):
/// - Type code
/// - Description
/// - Nature (Income or Expense)
/// - Sign (+ or -)
///
/// The sign field is used to determine if a transaction is a credit (+) or debit (-).
/// This data-driven approach means code automatically adapts if database changes.
/// </summary>
public class TransactionType
{
    /// <summary>
    /// Transaction type code (1-9 from CNAB spec)
    /// </summary>
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description (e.g., "Débito", "Boleto", "Financiamento")
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Transaction nature: "Income" or "Expense"
    /// </summary>
    public string Nature { get; set; } = string.Empty;

    /// <summary>
    /// Sign indicator: "+" for credit (income), "-" for debit (expense)
    /// This is the source of truth for signed amount calculation
    /// </summary>
    public string Sign { get; set; } = string.Empty;

    /// <summary>
    /// Calculate signed amount multiplier based on sign from database
    /// </summary>
    /// <returns>1.0m for credit (+), -1.0m for debit (-)</returns>
    public decimal GetSignMultiplier() =>
        Sign switch
        {
            "+" => 1.0m,
            "-" => -1.0m,
            _ => throw new InvalidOperationException($"Invalid sign value: {Sign}. Must be '+' or '-'.")
        };

    /// <summary>
    /// Check if this type is a credit (income)
    /// </summary>
    public bool IsCredit() => Sign == "+";

    /// <summary>
    /// Check if this type is a debit (expense)
    /// </summary>
    public bool IsDebit() => Sign == "-";
}
