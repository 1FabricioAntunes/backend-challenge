namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Service to retrieve transaction type information from the transaction_types lookup table.
/// 
/// This decouples the code from hardcoded logic:
/// - Signs (+ for credit, - for debit) are stored in database
/// - If database changes, code automatically adapts
/// - No need to recompile or restart to change sign logic
/// </summary>
public interface ITransactionTypeService
{
    /// <summary>
    /// Get the sign for a transaction type from the database.
    /// </summary>
    /// <param name="typeCode">Transaction type code (1-9)</param>
    /// <returns>Sign: "+" for credit/income, "-" for debit/expense</returns>
    /// <exception cref="ArgumentException">If type code is invalid or not found in database</exception>
    Task<string> GetSignAsync(string typeCode);

    /// <summary>
    /// Check if a transaction type is a credit (income) type
    /// </summary>
    /// <param name="typeCode">Transaction type code (1-9)</param>
    /// <returns>True if type is credit/income, false if debit/expense</returns>
    Task<bool> IsCreditAsync(string typeCode);

    /// <summary>
    /// Get the nature of a transaction type (Income or Expense)
    /// </summary>
    /// <param name="typeCode">Transaction type code (1-9)</param>
    /// <returns>Nature: "Income" or "Expense"</returns>
    Task<string> GetNatureAsync(string typeCode);
}
