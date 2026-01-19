namespace TransactionProcessor.Application.DTOs;

/// <summary>
/// Transaction list item returned by transactions query endpoint.
/// </summary>
public class TransactionDto
{
    /// <summary>
    /// Transaction identifier (BIGSERIAL ID as long).
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Store code (using StoreId string representation for normalized schema).
    /// </summary>
    public string StoreCode { get; set; } = string.Empty;

    /// <summary>
    /// Store display name.
    /// </summary>
    public string StoreName { get; set; } = string.Empty;

    /// <summary>
    /// Transaction type code (1-9 from CNAB spec).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Signed amount in BRL (credits positive, debits negative).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Occurrence timestamp (UTC) combined from date and time fields.
    /// Serializes as ISO 8601.
    /// </summary>
    public DateTime Date { get; set; }
}
