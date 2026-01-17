namespace TransactionProcessor.Domain.ValueObjects;

/// <summary>
/// Transaction type codes from CNAB specification (1-9).
/// These codes must match the transaction_types lookup table in the database.
/// 
/// Each type has a sign that determines if it's a credit (+) or debit (-).
/// The sign must be retrieved from the database lookup table, not hardcoded.
///
/// If the database lookup table changes (e.g., type 1 becomes debit instead of credit),
/// the code automatically adapts by querying the database for the current sign.
/// </summary>
public static class TransactionTypeCode
{
    /// <summary>
    /// Transaction type 1: Débito (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string Debit = "1";

    /// <summary>
    /// Transaction type 2: Boleto (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string Boleto = "2";

    /// <summary>
    /// Transaction type 3: Financiamento (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string Financing = "3";

    /// <summary>
    /// Transaction type 4: Crédito (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string Credit = "4";

    /// <summary>
    /// Transaction type 5: Recebimento Empr. (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string CompanyReceipt = "5";

    /// <summary>
    /// Transaction type 6: Vendas (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string Sales = "6";

    /// <summary>
    /// Transaction type 7: Recebimento TED (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string TEDReceipt = "7";

    /// <summary>
    /// Transaction type 8: Recebimento DOC (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string DOCReceipt = "8";

    /// <summary>
    /// Transaction type 9: Aluguel (CNAB spec)
    /// Sign is determined by database lookup, not hardcoded
    /// </summary>
    public const string Rent = "9";

    /// <summary>
    /// All valid transaction type codes (1-9)
    /// </summary>
    public static IReadOnlyList<string> AllTypeCodes =>
        new[] { Debit, Boleto, Financing, Credit, CompanyReceipt, Sales, TEDReceipt, DOCReceipt, Rent };

    /// <summary>
    /// Check if a type code is valid
    /// </summary>
    public static bool IsValid(string typeCode) =>
        AllTypeCodes.Contains(typeCode);
}
