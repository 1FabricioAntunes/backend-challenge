namespace TransactionProcessor.Application.Models;

/// <summary>
/// Represents a single parsed CNAB transaction line (80 characters fixed-width format).
/// </summary>
public class CNABLineData
{
    /// <summary>
    /// Transaction type (1-9) determining if credit or debit.
    /// 1,4,5,6,7,8 = Inflow (Debit)
    /// 2,3,9 = Outflow (Credit)
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    /// Transaction date in YYYYMMDD format, parsed to DateTime.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Transaction amount in cents. Must be divided by 100 to get actual value.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Recipient CPF (11 digits).
    /// </summary>
    public string CPF { get; set; } = string.Empty;

    /// <summary>
    /// Card used in transaction (12 characters).
    /// </summary>
    public string Card { get; set; } = string.Empty;

    /// <summary>
    /// Transaction time in HHMMSS format, parsed to TimeSpan.
    /// </summary>
    public TimeSpan Time { get; set; }

    /// <summary>
    /// Store representative/owner name (14 characters).
    /// </summary>
    public string StoreOwner { get; set; } = string.Empty;

    /// <summary>
    /// Store name (19 characters). Used to identify or lookup store.
    /// </summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// Calculated signed amount: positive for inflow, negative for outflow.
    /// </summary>
    public decimal SignedAmount
    {
        get
        {
            // Inflow types (1,4,5,6,7,8): positive
            // Outflow types (2,3,9): negative
            if (Type is 1 or 4 or 5 or 6 or 7 or 8)
                return Amount / 100m;
            if (Type is 2 or 3 or 9)
                return -(Amount / 100m);
            
            throw new InvalidOperationException($"Invalid transaction type: {Type}");
        }
    }
}

/// <summary>
/// Represents validation results from CNAB parsing.
/// </summary>
public class CNABValidationResult
{
    /// <summary>
    /// Indicates if all lines parsed and validated successfully.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Collection of parsed valid lines.
    /// </summary>
    public List<CNABLineData> ValidLines { get; set; } = new();

    /// <summary>
    /// Collection of validation errors encountered during parsing.
    /// Format: "Line {lineNumber}: {error message}"
    /// </summary>
    public List<string> Errors { get; set; } = new();
}
