namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Domain service for calculating signed transaction amounts based on CNAB type rules.
/// 
/// This service encapsulates the business logic for determining whether a transaction
/// increases (credit) or decreases (debit) a store's balance.
/// 
/// CNAB Specification Reference (80-character fixed-width format):
/// Position 1 contains the transaction type code (1-9), which determines the nature
/// of the transaction and its impact on the store's balance.
/// 
/// Credit Types (Increase Balance):
/// - Type 4: Crédito (Credit) - Direct credit transaction
/// - Type 5: Recebimento Empréstimo (Loan Receipt) - Inflow from loan
/// - Type 6: Vendas (Sales) - Sales proceeds
/// - Type 7: Recebimento TED (TED Receipt) - Electronic transfer received
/// - Type 8: Recebimento DOC (DOC Receipt) - Document transfer received
/// 
/// Debit Types (Decrease Balance):
/// - Type 1: Débito (Debit) - Direct debit transaction
/// - Type 2: Boleto (Bank Slip) - Payment via bank slip (outflow)
/// - Type 3: Financiamento (Financing) - Financing charge (outflow)
/// - Type 9: Aluguel (Rent) - Rent payment (outflow)
/// 
/// Design Rationale:
/// This service is stateless and delegates to the Transaction entity's GetSignedAmount method,
/// providing a consistent interface for calculating signed amounts throughout the application.
/// 
/// Reference: docs/business-rules.md § Transaction Types table
/// Reference: CNAB 240 specification (position 1 = type code)
/// </summary>
public interface ISignedAmountCalculator
{
    /// <summary>
    /// Calculate the signed amount for a transaction based on its type.
    /// 
    /// Algorithm:
    /// 1. Convert amount from cents to BRL: Amount / 100.00
    /// 2. Retrieve sign multiplier from TransactionType entity (+1 for credit, -1 for debit)
    /// 3. Calculate signed amount: (Amount / 100.00) × SignMultiplier
    /// 4. Return result (positive for credits, negative for debits)
    /// 
    /// Examples:
    /// 
    /// Credit Transaction (Type 6 - Sales):
    ///   Amount: 25000 cents = 250.00 BRL
    ///   Type: 6 (Sign = '+')
    ///   Result: +250.00 BRL (increases balance)
    /// 
    /// Debit Transaction (Type 2 - Boleto):
    ///   Amount: 15000 cents = 150.00 BRL
    ///   Type: 2 (Sign = '-')
    ///   Result: -150.00 BRL (decreases balance)
    /// 
    /// </summary>
    /// <param name="transaction">The transaction to calculate signed amount for. Must have TransactionType loaded.</param>
    /// <returns>
    /// Positive decimal for credit transactions (Types 4, 5, 6, 7, 8),
    /// negative decimal for debit transactions (Types 1, 2, 3, 9)
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if transaction or transaction.TransactionType is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if TransactionType.Sign has an invalid value</exception>
    /// <remarks>
    /// CNAB Business Rule: The sign is determined by the transaction type's Sign field
    /// from the transaction_types lookup table in the database, ensuring a single source
    /// of truth for credit/debit classification.
    /// 
    /// TransactionType must be eager-loaded when querying transactions to avoid N+1 queries
    /// and ensure the Sign value is available for calculation.
    /// 
    /// See: Transaction.GetSignedAmount(TransactionType) for underlying implementation
    /// See: docs/business-rules.md § CNAB File Format for complete type specifications
    /// </remarks>
    decimal Calculate(Entities.Transaction transaction);
}
