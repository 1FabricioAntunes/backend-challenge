namespace TransactionProcessor.Application.DTOs;

/// <summary>
/// Store summary with computed balance for query responses.
/// </summary>
public class StoreDto
{
    /// <summary>
    /// Store code (normalized to Id string for now).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Store display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current balance in BRL (signed sum of transactions).
    /// </summary>
    public decimal Balance { get; set; }
}
