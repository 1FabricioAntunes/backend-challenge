namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Domain service implementation for calculating signed transaction amounts.
/// 
/// This stateless service encapsulates the CNAB business rule for determining
/// whether a transaction is a credit (positive) or debit (negative) based on
/// the transaction type code.
/// 
/// CNAB Type Mapping (80-character fixed-width format, position 1):
/// 
/// CREDIT TYPES (Sign = '+', result is positive):
/// ┌──────┬─────────────────────────┬──────────┬────────┐
/// │ Type │ Description             │ Nature   │ Sign   │
/// ├──────┼─────────────────────────┼──────────┼────────┤
/// │  4   │ Crédito                 │ Inflow   │   +    │
/// │  5   │ Recebimento Empréstimo  │ Inflow   │   +    │
/// │  6   │ Vendas                  │ Inflow   │   +    │
/// │  7   │ Recebimento TED         │ Inflow   │   +    │
/// │  8   │ Recebimento DOC         │ Inflow   │   +    │
/// └──────┴─────────────────────────┴──────────┴────────┘
/// 
/// DEBIT TYPES (Sign = '-', result is negative):
/// ┌──────┬─────────────────────────┬──────────┬────────┐
/// │ Type │ Description             │ Nature   │ Sign   │
/// ├──────┼─────────────────────────┼──────────┼────────┤
/// │  1   │ Débito                  │ Outflow  │   -    │
/// │  2   │ Boleto                  │ Outflow  │   -    │
/// │  3   │ Financiamento           │ Outflow  │   -    │
/// │  9   │ Aluguel                 │ Outflow  │   -    │
/// └──────┴─────────────────────────┴──────────┴────────┘
/// 
/// Database-Driven Design:
/// The sign values are retrieved from the transaction_types lookup table,
/// NOT hardcoded in this service. This ensures:
/// - Changes to business rules only require database updates
/// - Consistent behavior across the entire application
/// - Audit trail for type classification changes
/// - No code recompilation for business rule changes
/// 
/// Reference: docs/business-rules.md § Transaction Types (complete CNAB mappings)
/// Reference: CNAB 240 specification (80-character format)
/// </summary>
public class SignedAmountCalculator : ISignedAmountCalculator
{
    /// <summary>
    /// Calculate the signed amount for a transaction based on its CNAB type.
    /// 
    /// Implementation:
    /// This service delegates to Transaction.GetSignedAmount(TransactionType) to ensure
    /// consistent calculation logic throughout the domain. The TransactionType entity
    /// contains the sign value ('+' or '-') from the database lookup table.
    /// 
    /// CNAB Business Logic:
    /// - Credit types (4, 5, 6, 7, 8): Return positive amount (increases store balance)
    /// - Debit types (1, 2, 3, 9): Return negative amount (decreases store balance)
    /// 
    /// Example Calculations:
    /// 
    /// Type 6 (Vendas/Sales - Credit):
    ///   Input: Transaction with Amount = 25000 cents, Type = 6 (Sign = '+')
    ///   Calculation: 25000 / 100 × (+1) = 250.00 BRL
    ///   Result: +250.00 (increases balance)
    /// 
    /// Type 2 (Boleto - Debit):
    ///   Input: Transaction with Amount = 15000 cents, Type = 2 (Sign = '-')
    ///   Calculation: 15000 / 100 × (-1) = -150.00 BRL
    ///   Result: -150.00 (decreases balance)
    /// 
    /// Type 7 (TED Receipt - Credit):
    ///   Input: Transaction with Amount = 500000 cents, Type = 7 (Sign = '+')
    ///   Calculation: 500000 / 100 × (+1) = 5000.00 BRL
    ///   Result: +5000.00 (increases balance)
    /// 
    /// Type 3 (Financiamento - Debit):
    ///   Input: Transaction with Amount = 100000 cents, Type = 3 (Sign = '-')
    ///   Calculation: 100000 / 100 × (-1) = -1000.00 BRL
    ///   Result: -1000.00 (decreases balance)
    /// 
    /// </summary>
    /// <param name="transaction">The transaction to calculate signed amount for. Must have TransactionType eager-loaded.</param>
    /// <returns>Signed amount in BRL (positive for credits, negative for debits)</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if transaction is null, or if transaction.TransactionType is null (not loaded from database)
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if TransactionType.Sign has an invalid value (not '+' or '-')
    /// </exception>
    /// <remarks>
    /// Performance Consideration:
    /// TransactionType must be eager-loaded using .Include(t => t.TransactionType) when
    /// querying transactions from the database. Failure to do so will result in:
    /// - Null reference exception (TransactionType is null)
    /// - N+1 query problem if lazy loading is enabled
    /// 
    /// Database Source of Truth:
    /// The Sign field comes from the transaction_types lookup table in PostgreSQL.
    /// This architecture allows business rule changes without code changes:
    /// 
    /// UPDATE transaction_types SET sign = '+' WHERE type_code = '1';
    /// -- Changes Type 1 from debit to credit without redeployment
    /// 
    /// Usage Pattern:
    /// var signedAmount = _signedAmountCalculator.Calculate(transaction);
    /// storeBalance += signedAmount; // Positive adds, negative subtracts
    /// 
    /// See: Transaction.GetSignedAmount(TransactionType) for detailed algorithm
    /// See: TransactionType.GetSignMultiplier() for sign-to-multiplier conversion
    /// </remarks>
    public decimal Calculate(Entities.Transaction transaction)
    {
        if (transaction == null)
            throw new ArgumentNullException(nameof(transaction), "Transaction cannot be null");

        if (transaction.TransactionType == null)
            throw new ArgumentNullException(
                nameof(transaction.TransactionType),
                $"Transaction {transaction.Id} has null TransactionType. " +
                "Ensure TransactionType is eager-loaded using .Include(t => t.TransactionType) " +
                "when querying transactions from the database.");

        // Delegate to the Transaction entity's business logic method
        // This ensures consistent calculation across the domain
        return transaction.GetSignedAmount(transaction.TransactionType);
    }
}
