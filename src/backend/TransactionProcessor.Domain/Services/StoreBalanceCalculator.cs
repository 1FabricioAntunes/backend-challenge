namespace TransactionProcessor.Domain.Services;

/// <summary>
/// Domain service implementation for calculating store balances from transactions.
/// 
/// This service aggregates signed transaction amounts to compute the net balance
/// for a store. It delegates to ISignedAmountCalculator for individual transaction
/// calculations, ensuring consistent logic across the domain.
/// 
/// CNAB Balance Calculation Rules:
/// 
/// Starting Balance: 0.00 BRL (all stores begin with zero balance)
/// 
/// Credit Transactions (Increase Balance):
/// ┌──────┬─────────────────────────┬────────────────────────┐
/// │ Type │ Description             │ Balance Impact         │
/// ├──────┼─────────────────────────┼────────────────────────┤
/// │  4   │ Crédito                 │ +Amount                │
/// │  5   │ Recebimento Empréstimo  │ +Amount                │
/// │  6   │ Vendas                  │ +Amount                │
/// │  7   │ Recebimento TED         │ +Amount                │
/// │  8   │ Recebimento DOC         │ +Amount                │
/// └──────┴─────────────────────────┴────────────────────────┘
/// 
/// Debit Transactions (Decrease Balance):
/// ┌──────┬─────────────────────────┬────────────────────────┐
/// │ Type │ Description             │ Balance Impact         │
/// ├──────┼─────────────────────────┼────────────────────────┤
/// │  1   │ Débito                  │ -Amount                │
/// │  2   │ Boleto                  │ -Amount                │
/// │  3   │ Financiamento           │ -Amount                │
/// │  9   │ Aluguel                 │ -Amount                │
/// └──────┴─────────────────────────┴────────────────────────┘
/// 
/// Real-World Calculation Example:
/// 
/// Store: "MERCADO DA AVENIDA"
/// File: CNAB0001.txt with 8 transactions
/// 
/// Transaction History:
/// 1. Type 6 (Vendas): 50000 cents = 500.00 BRL → +500.00
/// 2. Type 1 (Débito): 15000 cents = 150.00 BRL → -150.00
/// 3. Type 4 (Crédito): 30000 cents = 300.00 BRL → +300.00
/// 4. Type 2 (Boleto): 10000 cents = 100.00 BRL → -100.00
/// 5. Type 7 (TED): 75000 cents = 750.00 BRL → +750.00
/// 6. Type 3 (Financiamento): 25000 cents = 250.00 BRL → -250.00
/// 7. Type 6 (Vendas): 40000 cents = 400.00 BRL → +400.00
/// 8. Type 9 (Aluguel): 20000 cents = 200.00 BRL → -200.00
/// 
/// Balance Calculation:
/// +500.00 + (-150.00) + (+300.00) + (-100.00) + (+750.00) + (-250.00) + (+400.00) + (-200.00)
/// = +500.00 - 150.00 + 300.00 - 100.00 + 750.00 - 250.00 + 400.00 - 200.00
/// = +1250.00 BRL
/// 
/// Final Balance: 1,250.00 BRL (positive, store has funds)
/// 
/// Negative Balance Example:
/// 
/// Store: "LOJA XYZ"
/// Transaction History:
/// 1. Type 6 (Vendas): 20000 cents = 200.00 BRL → +200.00
/// 2. Type 2 (Boleto): 35000 cents = 350.00 BRL → -350.00
/// 3. Type 3 (Financiamento): 15000 cents = 150.00 BRL → -150.00
/// 
/// Balance Calculation:
/// +200.00 + (-350.00) + (-150.00) = -300.00 BRL
/// 
/// Final Balance: -300.00 BRL (negative, store owes money or has pending debits)
/// 
/// Reference: docs/business-rules.md § Store Balance Calculation
/// Reference: docs/async-processing.md § Processing Pipeline (balance update step)
/// </summary>
public class StoreBalanceCalculator : IStoreBalanceCalculator
{
    private readonly ISignedAmountCalculator _signedAmountCalculator;

    /// <summary>
    /// Constructor with dependency injection.
    /// </summary>
    /// <param name="signedAmountCalculator">
    /// Service to calculate signed amounts for individual transactions.
    /// Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if signedAmountCalculator is null
    /// </exception>
    public StoreBalanceCalculator(ISignedAmountCalculator signedAmountCalculator)
    {
        _signedAmountCalculator = signedAmountCalculator 
            ?? throw new ArgumentNullException(nameof(signedAmountCalculator));
    }

    /// <summary>
    /// Calculate the total balance for a store from its transaction history.
    /// 
    /// Implementation Details:
    /// 1. Start with balance = 0.00
    /// 2. For each transaction:
    ///    a. Validate transaction is not null
    ///    b. Calculate signed amount via ISignedAmountCalculator
    ///    c. Add signed amount to running total
    /// 3. Return final balance
    /// 
    /// Performance Characteristics:
    /// - Time Complexity: O(n) where n = transaction count
    /// - Space Complexity: O(1) - only stores running total
    /// - No database queries (operates on in-memory list)
    /// - No external API calls
    /// 
    /// Error Handling:
    /// - Null transaction list: throws ArgumentNullException
    /// - Null transaction in list: throws ArgumentNullException with index
    /// - Missing TransactionType: throws InvalidOperationException from ISignedAmountCalculator
    /// - Invalid sign values: throws InvalidOperationException from TransactionType.GetSignMultiplier
    /// 
    /// </summary>
    /// <param name="transactions">
    /// List of transactions for a single store. TransactionType must be eager-loaded.
    /// Can be empty list (returns 0.00).
    /// </param>
    /// <returns>
    /// Total balance in BRL. Positive for net credits, negative for net debits, zero if balanced.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if transactions list is null, or if any transaction in the list is null
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if any transaction has null TransactionType navigation property,
    /// or if TransactionType.Sign has invalid value
    /// </exception>
    /// <remarks>
    /// Usage Pattern During File Processing:
    /// 
    /// Step 1: Retrieve all transactions for store (after persisting new file's transactions)
    /// var allTransactions = await _transactionRepository
    ///     .GetByStoreIdAsync(storeId)
    ///     .Include(t => t.TransactionType); // CRITICAL: Eager-load TransactionType
    /// 
    /// Step 2: Calculate new balance
    /// var newBalance = _storeBalanceCalculator.CalculateBalance(allTransactions);
    /// 
    /// Step 3: Update store entity
    /// store.UpdateBalance(newBalance);
    /// 
    /// Step 4: Persist to database
    /// await _storeRepository.UpdateAsync(store);
    /// 
    /// Optimization - Incremental Balance Update:
    /// For better performance during file processing, calculate balance incrementally
    /// instead of re-aggregating all historical transactions:
    /// 
    /// // Get current balance
    /// var currentBalance = store.Balance;
    /// 
    /// // Add signed amounts from new file's transactions only
    /// foreach (var newTransaction in newFileTransactions)
    /// {
    ///     var signedAmount = _signedAmountCalculator.Calculate(newTransaction);
    ///     currentBalance += signedAmount;
    /// }
    /// 
    /// // Update store with new balance
    /// store.UpdateBalance(currentBalance);
    /// 
    /// This optimization reduces O(total transactions) to O(new file transactions).
    /// 
    /// Testing Considerations:
    /// - Test empty transaction list (should return 0.00)
    /// - Test single transaction (credit and debit cases)
    /// - Test multiple transactions of same type
    /// - Test mixed credit/debit transactions
    /// - Test large transaction counts (performance)
    /// - Test null handling (list null, transaction null, TransactionType null)
    /// - Test balance precision (decimal arithmetic)
    /// 
    /// See: ISignedAmountCalculator.Calculate for individual transaction logic
    /// See: Store.UpdateBalance for balance persistence constraints
    /// See: Transaction.GetSignedAmount for underlying calculation method
    /// </remarks>
    public decimal CalculateBalance(List<Entities.Transaction> transactions)
    {
        if (transactions == null)
            throw new ArgumentNullException(nameof(transactions), "Transaction list cannot be null");

        // Start with zero balance (default for new stores)
        decimal balance = 0m;

        // Aggregate signed amounts from all transactions
        for (int i = 0; i < transactions.Count; i++)
        {
            var transaction = transactions[i];

            if (transaction == null)
                throw new ArgumentNullException(
                    nameof(transactions),
                    $"Transaction at index {i} is null. All transactions in the list must be non-null.");

            try
            {
                // Calculate signed amount for this transaction
                // Positive for credits (Types 4,5,6,7,8), negative for debits (Types 1,2,3,9)
                var signedAmount = _signedAmountCalculator.Calculate(transaction);

                // Add to running balance total
                balance += signedAmount;
            }
            catch (ArgumentNullException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to calculate balance: Transaction {transaction.Id} (index {i}) has missing TransactionType. " +
                    $"Ensure TransactionType is eager-loaded using .Include(t => t.TransactionType) when querying transactions.",
                    ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to calculate balance: Transaction {transaction.Id} (index {i}, Type {transaction.TransactionTypeCode}) " +
                    $"has invalid TransactionType.Sign value. Valid values are '+' (credit) or '-' (debit).",
                    ex);
            }
        }

        return balance;
    }
}
