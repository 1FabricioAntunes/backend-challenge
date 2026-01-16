namespace TransactionProcessor.Domain.ValueObjects;

/// <summary>
/// Represents the status of a processed file.
/// </summary>
public enum FileStatus
{
    Uploaded = 0,
    Processing = 1,
    Processed = 2,
    Rejected = 3
}
