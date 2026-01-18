namespace TransactionProcessor.Domain.Interfaces;

/// <summary>
/// Represents the result of a notification operation.
/// </summary>
public class NotificationResult
{
    /// <summary>
    /// Indicates whether the notification was sent successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Reason for failure (null if Success is true).
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Number of attempts made to send the notification.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Notification ID for tracking and reference.
    /// </summary>
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Correlation ID for tracking across related operations.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Static factory method for successful notification result.
    /// </summary>
    public static NotificationResult SuccessResult(Guid notificationId, string correlationId, int attemptCount)
    {
        return new NotificationResult
        {
            Success = true,
            NotificationId = notificationId,
            CorrelationId = correlationId,
            AttemptCount = attemptCount
        };
    }

    /// <summary>
    /// Static factory method for failed notification result.
    /// </summary>
    public static NotificationResult FailureResult(Guid notificationId, string correlationId, string failureReason, int attemptCount)
    {
        return new NotificationResult
        {
            Success = false,
            FailureReason = failureReason,
            NotificationId = notificationId,
            CorrelationId = correlationId,
            AttemptCount = attemptCount
        };
    }
}

/// <summary>
/// Represents configuration for notification retry policies.
/// </summary>
public class NotificationRetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts before sending to DLQ.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before first retry.
    /// </summary>
    public int InitialDelayMs { get; set; } = 2000;

    /// <summary>
    /// Multiplier for exponential backoff (each retry delay = previous * multiplier).
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to send failed notifications to DLQ after max retries.
    /// </summary>
    public bool SendFailedToNotificationDlq { get; set; } = true;
}

/// <summary>
/// Abstraction for notification operations with retry and DLQ support.
/// Responsible for notifying users of file processing completion or failure.
/// Implements exponential backoff retry with fallback to Dead Letter Queue (DLQ).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Send notification for successful file processing completion.
    /// </summary>
    /// <param name="fileId">Unique identifier of the processed file</param>
    /// <param name="status">Final file status (Processed or Rejected)</param>
    /// <param name="details">Additional details about the processing result (transaction count, errors, etc.)</param>
    /// <param name="correlationId">Correlation ID for tracking across related operations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>NotificationResult indicating success/failure and attempt count</returns>
    /// <remarks>
    /// Implementation Strategy:
    /// 1. Create notification record in database for idempotency
    /// 2. Attempt to send notification using configured methods (Email, Webhook)
    /// 3. On transient failure: retry with exponential backoff (3 attempts: 2s, 4s, 8s)
    /// 4. On persistent failure after max retries: publish to notification-dlq SQS queue
    /// 5. Log all attempts with correlation ID for debugging
    /// 6. On success: mark notification as sent in database
    /// 
    /// Fallback: If notification fails, user can poll GET /api/files/v1/{id} for status
    /// </remarks>
    Task<NotificationResult> NotifyProcessingCompletedAsync(
        Guid fileId,
        string status,
        string details,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification for file processing failure.
    /// </summary>
    /// <param name="fileId">Unique identifier of the failed file</param>
    /// <param name="errorMessage">Error message describing why processing failed</param>
    /// <param name="correlationId">Correlation ID for tracking across related operations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>NotificationResult indicating success/failure and attempt count</returns>
    /// <remarks>
    /// Uses same retry logic and DLQ fallback as NotifyProcessingCompletedAsync.
    /// Error messages should include line number or specific validation failure for user clarity.
    /// </remarks>
    Task<NotificationResult> NotifyProcessingFailedAsync(
        Guid fileId,
        string errorMessage,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retry a failed notification from the Notification Dead Letter Queue.
    /// </summary>
    /// <param name="notificationId">Unique identifier of the notification to retry</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>NotificationResult indicating success/failure of retry attempt</returns>
    /// <remarks>
    /// Called by NotificationDlqWorker background service to retry failed notifications.
    /// Max 3 attempts from DLQ; after that requires manual review.
    /// 
    /// DLQ Scenarios:
    /// - SMTP connection failures
    /// - Webhook endpoint timeouts
    /// - Invalid email addresses (non-retryable, manual review)
    /// - Network failures (retryable)
    /// 
    /// Manual Review Workflow:
    /// 1. Alert on DLQ depth > 0
    /// 2. Review failed notification details
    /// 3. Fix underlying issue (SMTP config, webhook endpoint, etc.)
    /// 4. Manually retry failed notification from DLQ
    /// </remarks>
    Task<NotificationResult> RetryFailedNotificationAsync(
        Guid notificationId,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current retry policy configuration.
    /// </summary>
    /// <returns>NotificationRetryPolicy with max retries, delays, and DLQ configuration</returns>
    NotificationRetryPolicy GetRetryPolicy();
}
