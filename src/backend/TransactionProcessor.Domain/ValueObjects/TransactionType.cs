namespace TransactionProcessor.Domain.ValueObjects;

/// <summary>
/// CNAB transaction types mapped to credit/debit semantics.
/// </summary>
public enum TransactionType
{
    Credit = 1,
    Debit = 2
}
