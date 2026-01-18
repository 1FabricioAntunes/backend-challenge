namespace TransactionProcessor.Infrastructure.Storage;

/// <summary>
/// Exception thrown when a file storage operation fails.
/// </summary>
public class StorageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the StorageException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StorageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageException class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
