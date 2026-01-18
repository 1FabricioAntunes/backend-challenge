namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Domain service for calculating store balances from transaction history.
/// 
/// This service aggregates all transactions for a store and computes the total balance
/// by summing the signed amounts. The balance represents the net financial position
/// of a store after considering all credit and debit transactions.
/// 
/// Balance Calculation Logic:
/// Balance = Σ (Transaction Signed Amounts)
/// Where:
/// - Credit transactions (Types 4, 5, 6, 7, 8) contribute positive amounts
/// - Debit transactions (Types 1, 2, 3, 9) contribute negative amounts
/// 
/// Example Calculation:
/// Given transactions for "MERCADO DA AVENIDA":
/// 1. Type 6 (Vendas/Sales): +250.00 BRL
/// 2. Type 2 (Boleto): -150.00 BRL
/// 3. Type 4 (Crédito): +100.00 BRL
/// 4. Type 3 (Financiamento): -50.00 BRL
/// 5. Type 7 (TED Receipt): +500.00 BRL
/// 
/// Balance = 250.00 + (-150.00) + 100.00 + (-50.00) + 500.00 = 650.00 BRL
/// 
/// Usage Context:
/// This service is invoked during CNAB file processing after all transactions
/// have been persisted to the database. The calculated balance is then stored
/// in the Store entity for quick retrieval without re-aggregating transactions.
/// 
/// Design Rationale:
/// - Encapsulates balance calculation logic in a single, testable service
/// - Depends on ISignedAmountCalculator for consistent signed amount calculation
/// - Stateless design allows safe concurrent usage across multiple file processing operations
/// - Single Responsibility: Only responsible for balance aggregation, not persistence
/// 
/// Reference: docs/business-rules.md § Store Balance Calculation
/// Reference: docs/async-processing.md § File Processing Pipeline (Step 7: Update balances)
/// </summary>
public interface IStoreBalanceCalculator
{
    /// <summary>
    /// Calculate the total balance for a store from its transaction history.
    /// 
    /// Algorithm:
    /// 1. For each transaction in the list:
    ///    a. Calculate signed amount using ISignedAmountCalculator
    ///    b. Add signed amount to running total
    /// 2. Return final aggregated balance
    /// 
    /// Transaction Processing Order:
    /// The calculation is order-independent since addition is commutative.
    /// Transactions can be provided in any order and will produce the same result.
    /// 
    /// Examples:
    /// 
    /// Example 1 - Single Store, Mixed Transactions:
    ///   Input: List of 5 transactions for Store "ABC"
    ///     - Transaction 1: Type 6, Amount 25000 cents → +250.00 BRL
    ///     - Transaction 2: Type 2, Amount 15000 cents → -150.00 BRL
    ///     - Transaction 3: Type 4, Amount 10000 cents → +100.00 BRL
    ///     - Transaction 4: Type 1, Amount 5000 cents → -50.00 BRL
    ///     - Transaction 5: Type 7, Amount 50000 cents → +500.00 BRL
    ///   Calculation: 250 + (-150) + 100 + (-50) + 500 = 650.00 BRL
    ///   Result: 650.00
    /// 
    /// Example 2 - All Credit Transactions:
    ///   Input: List of 3 transactions
    ///     - Transaction 1: Type 4, Amount 10000 cents → +100.00 BRL
    ///     - Transaction 2: Type 6, Amount 20000 cents → +200.00 BRL
    ///     - Transaction 3: Type 8, Amount 30000 cents → +300.00 BRL
    ///   Calculation: 100 + 200 + 300 = 600.00 BRL
    ///   Result: 600.00
    /// 
    /// Example 3 - All Debit Transactions:
    ///   Input: List of 2 transactions
    ///     - Transaction 1: Type 1, Amount 15000 cents → -150.00 BRL
    ///     - Transaction 2: Type 3, Amount 25000 cents → -250.00 BRL
    ///   Calculation: (-150) + (-250) = -400.00 BRL
    ///   Result: -400.00 (negative balance, store owes money)
    /// 
    /// Example 4 - Empty Transaction List:
    ///   Input: Empty list
    ///   Result: 0.00 (initial balance is zero)
    /// 
    /// </summary>
    /// <param name="transactions">
    /// List of transactions for a single store. Must have TransactionType navigation property loaded.
    /// Empty list is valid and returns 0.00.
    /// </param>
    /// <returns>
    /// Aggregated balance in BRL. Can be positive (store has funds), negative (store owes),
    /// or zero (balanced or no transactions).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if transactions list is null, or if any transaction in the list is null
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if any transaction lacks a loaded TransactionType navigation property
    /// (required for signed amount calculation)
    /// </exception>
    /// <remarks>
    /// Performance Considerations:
    /// - This method iterates through all transactions once (O(n) complexity)
    /// - For large transaction counts (10,000+), consider batching or database aggregation
    /// - TransactionType must be eager-loaded to avoid N+1 query problems
    /// 
    /// Data Requirements:
    /// - All transactions must belong to the same store (not validated by this method)
    /// - TransactionType navigation property must be loaded for each transaction
    /// - Use .Include(t => t.TransactionType) when querying transactions from database
    /// 
    /// Store Balance Update Pattern:
    /// var transactions = await _transactionRepository.GetByStoreIdAsync(storeId);
    /// var balance = _storeBalanceCalculator.CalculateBalance(transactions);
    /// store.UpdateBalance(balance);
    /// await _storeRepository.UpdateAsync(store);
    /// 
    /// Incremental Balance Updates:
    /// For performance optimization during file processing, consider calculating
    /// balance incrementally as transactions are processed rather than re-aggregating
    /// all historical transactions:
    /// 
    /// var currentBalance = store.Balance;
    /// foreach (var newTransaction in newTransactions)
    /// {
    ///     var signedAmount = _signedAmountCalculator.Calculate(newTransaction);
    ///     currentBalance += signedAmount;
    /// }
    /// store.UpdateBalance(currentBalance);
    /// 
    /// Thread Safety:
    /// This method is stateless and thread-safe. Multiple threads can invoke it
    /// concurrently without synchronization concerns.
    /// 
    /// See: ISignedAmountCalculator for individual transaction amount calculation
    /// See: Store.UpdateBalance for balance persistence
    /// See: docs/business-rules.md § Store Balance Calculation for business rules
    /// </remarks>
    decimal CalculateBalance(List<Entities.Transaction> transactions);
}
