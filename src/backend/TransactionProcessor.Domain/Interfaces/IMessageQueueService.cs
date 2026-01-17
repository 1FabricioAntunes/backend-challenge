namespace TransactionProcessor.Domain.Interfaces;

/// <summary>
/// Represents a message received from a queue.
/// </summary>
/// <typeparam name="T">Message body type</typeparam>
public class QueueMessage<T>
{
    /// <summary>
    /// Unique identifier for the message in the queue.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Message body (deserialized).
    /// </summary>
    public T? Body { get; set; }

    /// <summary>
    /// Receipt handle for message acknowledgment/deletion.
    /// Used by consumer to delete message after processing.
    /// </summary>
    public string ReceiptHandle { get; set; } = string.Empty;

    /// <summary>
    /// Attributes attached to the message (timestamp, correlation ID, etc.).
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new();
}

/// <summary>
/// Abstraction for message queue operations (SQS).
/// Allows implementation to switch between AWS SQS, RabbitMQ, or other providers.
/// </summary>
public interface IMessageQueueService
{
    /// <summary>
    /// Publish a message to the queue
    /// </summary>
    /// <typeparam name="T">Message body type</typeparam>
    /// <param name="message">Message to publish</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Message ID returned by queue service</returns>
    Task<string> PublishAsync<T>(T message, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Receive a batch of messages from queue
    /// </summary>
    /// <typeparam name="T">Message body type</typeparam>
    /// <param name="maxNumberOfMessages">Maximum messages to receive (1-10)</param>
    /// <param name="visibilityTimeoutSeconds">Visibility timeout in seconds (time for processing)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of messages received from queue</returns>
    Task<IEnumerable<QueueMessage<T>>> ReceiveMessagesAsync<T>(
        int maxNumberOfMessages = 10,
        int visibilityTimeoutSeconds = 300,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete message from queue after processing
    /// </summary>
    /// <param name="receiptHandle">Receipt handle of message to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get messages from Dead Letter Queue (for monitoring/debugging)
    /// </summary>
    /// <typeparam name="T">Message body type</typeparam>
    /// <param name="maxNumberOfMessages">Maximum messages to receive</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of messages from DLQ</returns>
    Task<IEnumerable<QueueMessage<T>>> GetDLQMessagesAsync<T>(
        int maxNumberOfMessages = 10,
        CancellationToken cancellationToken = default);
}
