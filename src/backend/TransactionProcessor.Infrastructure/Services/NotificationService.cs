using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionProcessor.Domain.Interfaces;

namespace TransactionProcessor.Infrastructure.Services;

/// <summary>
/// Represents a notification message for the DLQ.
/// </summary>
internal class NotificationDlqMessage
{
    /// <summary>
    /// Unique notification identifier.
    /// </summary>
    [JsonPropertyName("notificationId")]
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Identifier of the file being processed.
    /// </summary>
    [JsonPropertyName("fileId")]
    public Guid FileId { get; set; }

    /// <summary>
    /// Type of notification (ProcessingCompleted or ProcessingFailed).
    /// </summary>
    [JsonPropertyName("notificationType")]
    public string NotificationType { get; set; } = string.Empty;

    /// <summary>
    /// Recipient email address.
    /// </summary>
    [JsonPropertyName("recipientEmail")]
    public string RecipientEmail { get; set; } = string.Empty;

    /// <summary>
    /// Number of attempts made to send this notification.
    /// </summary>
    [JsonPropertyName("attemptCount")]
    public int AttemptCount { get; set; }

    /// <summary>
    /// Timestamp of the last attempt.
    /// </summary>
    [JsonPropertyName("lastAttemptAt")]
    public DateTime LastAttemptAt { get; set; }

    /// <summary>
    /// Error message from the last failed attempt.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional context about the notification (status, details, etc.).
    /// </summary>
    [JsonPropertyName("context")]
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Implementation of INotificationService with retry policies and DLQ support.
/// Sends notifications with exponential backoff retry and fallback to Dead Letter Queue.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;
    private readonly IMessageQueueService _messageQueueService;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly NotificationRetryPolicy _retryPolicyConfig;

    /// <summary>
    /// Initializes a new instance of NotificationService.
    /// </summary>
    /// <param name="configuration">Configuration provider for email settings and DLQ URLs</param>
    /// <param name="logger">Logger for operation tracking</param>
    /// <param name="messageQueueService">Message queue service for DLQ publishing</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    public NotificationService(
        IConfiguration configuration,
        ILogger<NotificationService> logger,
        IMessageQueueService messageQueueService)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _messageQueueService = messageQueueService ?? throw new ArgumentNullException(nameof(messageQueueService));

        _retryPolicyConfig = CreateRetryPolicyConfig();
        _retryPolicy = BuildRetryPolicy();
    }

    /// <summary>
    /// Send notification for successful file processing completion.
    /// </summary>
    public async Task<NotificationResult> NotifyProcessingCompletedAsync(
        Guid fileId,
        string status,
        string details,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var notificationId = Guid.NewGuid();
        var attemptCount = 0;

        _logger.LogInformation(
            "Starting notification for file {FileId} with status {Status}. NotificationId: {NotificationId}, CorrelationId: {CorrelationId}",
            fileId,
            status,
            notificationId,
            correlationId);

        try
        {
            // Send notification with retry policy
            await _retryPolicy.ExecuteAsync(async () =>
            {
                attemptCount++;
                _logger.LogDebug(
                    "Notification attempt {AttemptCount} for NotificationId: {NotificationId}",
                    attemptCount,
                    notificationId);

                // Get recipient email (placeholder - would come from user profile in real implementation)
                var recipientEmail = GetRecipientEmail(fileId, correlationId);

                // Try multiple notification methods
                if (!await SendEmailNotificationAsync(recipientEmail, fileId, status, details, correlationId, cancellationToken))
                {
                    throw new NotificationException($"Failed to send email notification to {recipientEmail}");
                }
            });

            _logger.LogInformation(
                "Notification sent successfully for file {FileId} after {AttemptCount} attempt(s). NotificationId: {NotificationId}",
                fileId,
                attemptCount,
                notificationId);

            return NotificationResult.SuccessResult(notificationId, correlationId, attemptCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Notification failed after {AttemptCount} attempt(s) for file {FileId}. Publishing to notification-dlq. NotificationId: {NotificationId}",
                attemptCount,
                fileId,
                notificationId);

            // Publish to notification DLQ for later retry
            await PublishToNotificationDlqAsync(
                notificationId,
                fileId,
                "ProcessingCompleted",
                GetRecipientEmail(fileId, correlationId),
                attemptCount,
                ex.Message,
                new Dictionary<string, object> { { "status", status }, { "details", details } },
                correlationId,
                cancellationToken);

            return NotificationResult.FailureResult(
                notificationId,
                correlationId,
                $"Notification failed after {attemptCount} attempts: {ex.Message}",
                attemptCount);
        }
    }

    /// <summary>
    /// Send notification for file processing failure.
    /// </summary>
    public async Task<NotificationResult> NotifyProcessingFailedAsync(
        Guid fileId,
        string errorMessage,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var notificationId = Guid.NewGuid();
        var attemptCount = 0;

        _logger.LogInformation(
            "Starting failure notification for file {FileId}. NotificationId: {NotificationId}, CorrelationId: {CorrelationId}",
            fileId,
            notificationId,
            correlationId);

        try
        {
            // Send notification with retry policy
            await _retryPolicy.ExecuteAsync(async () =>
            {
                attemptCount++;
                _logger.LogDebug(
                    "Failure notification attempt {AttemptCount} for NotificationId: {NotificationId}",
                    attemptCount,
                    notificationId);

                var recipientEmail = GetRecipientEmail(fileId, correlationId);

                if (!await SendEmailNotificationAsync(
                    recipientEmail,
                    fileId,
                    "Rejected",
                    errorMessage,
                    correlationId,
                    cancellationToken))
                {
                    throw new NotificationException($"Failed to send failure notification to {recipientEmail}");
                }
            });

            _logger.LogInformation(
                "Failure notification sent successfully for file {FileId} after {AttemptCount} attempt(s). NotificationId: {NotificationId}",
                fileId,
                attemptCount,
                notificationId);

            return NotificationResult.SuccessResult(notificationId, correlationId, attemptCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failure notification failed after {AttemptCount} attempt(s) for file {FileId}. Publishing to notification-dlq. NotificationId: {NotificationId}",
                attemptCount,
                fileId,
                notificationId);

            // Publish to notification DLQ for later retry
            await PublishToNotificationDlqAsync(
                notificationId,
                fileId,
                "ProcessingFailed",
                GetRecipientEmail(fileId, correlationId),
                attemptCount,
                ex.Message,
                new Dictionary<string, object> { { "errorMessage", errorMessage } },
                correlationId,
                cancellationToken);

            return NotificationResult.FailureResult(
                notificationId,
                correlationId,
                $"Failure notification failed after {attemptCount} attempts: {ex.Message}",
                attemptCount);
        }
    }

    /// <summary>
    /// Retry a failed notification from the Notification Dead Letter Queue.
    /// Called by NotificationDlqWorker background service.
    /// </summary>
    public async Task<NotificationResult> RetryFailedNotificationAsync(
        Guid notificationId,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrying failed notification from DLQ. NotificationId: {NotificationId}, CorrelationId: {CorrelationId}",
            notificationId,
            correlationId);

        var attemptCount = 0;

        try
        {
            // Retry with same exponential backoff policy
            await _retryPolicy.ExecuteAsync(async () =>
            {
                attemptCount++;
                _logger.LogDebug(
                    "DLQ retry attempt {AttemptCount} for NotificationId: {NotificationId}",
                    attemptCount,
                    notificationId);

                // In production, this would load the notification details from database
                // and retry sending. For now, log the retry attempt.
                await Task.Delay(10, cancellationToken);
            });

            _logger.LogInformation(
                "DLQ notification retry successful after {AttemptCount} attempt(s). NotificationId: {NotificationId}",
                attemptCount,
                notificationId);

            return NotificationResult.SuccessResult(notificationId, correlationId, attemptCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "DLQ notification retry failed after {AttemptCount} attempt(s). NotificationId: {NotificationId} requires manual review",
                attemptCount,
                notificationId);

            return NotificationResult.FailureResult(
                notificationId,
                correlationId,
                $"DLQ retry failed after {attemptCount} attempts: {ex.Message}",
                attemptCount);
        }
    }

    /// <summary>
    /// Get the current retry policy configuration.
    /// </summary>
    public NotificationRetryPolicy GetRetryPolicy()
    {
        return _retryPolicyConfig;
    }

    /// <summary>
    /// Builds the Polly retry policy with exponential backoff.
    /// Delays: 2s, 4s, 8s (exponential backoff with multiplier 2.0)
    /// </summary>
    private IAsyncPolicy BuildRetryPolicy()
    {
        return Policy
            .Handle<NotificationException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: _retryPolicyConfig.MaxRetryAttempts,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Notification retry attempt {RetryCount} after {DelayMs}ms due to transient failure",
                        retryCount,
                        (int)timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Creates the retry policy configuration with default values.
    /// </summary>
    private NotificationRetryPolicy CreateRetryPolicyConfig()
    {
        return new NotificationRetryPolicy
        {
            MaxRetryAttempts = 3,
            InitialDelayMs = 2000,      // 2 seconds
            BackoffMultiplier = 2.0,    // Exponential: 2s, 4s, 8s
            SendFailedToNotificationDlq = true
        };
    }

    /// <summary>
    /// Send email notification (placeholder implementation).
    /// In production, this would integrate with SMTP service.
    /// </summary>
    private async Task<bool> SendEmailNotificationAsync(
        string recipientEmail,
        Guid fileId,
        string status,
        string details,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Sending email notification to {Email} for file {FileId} with status {Status}",
            recipientEmail,
            fileId,
            status);

        try
        {
            // Placeholder: In production, this would:
            // 1. Render email template with file status and details
            // 2. Send via SMTP service (SendGrid, AWS SES, etc.)
            // 3. Track delivery status
            // 4. Handle SMTP errors (connection timeout, auth failure, etc.)

            // For now, simulate successful send
            await Task.Delay(10, cancellationToken);

            _logger.LogDebug(
                "Email notification sent successfully to {Email} for file {FileId}",
                recipientEmail,
                fileId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send email notification to {Email} for file {FileId}",
                recipientEmail,
                fileId);
            return false;
        }
    }

    /// <summary>
    /// Send webhook notification (placeholder for future implementation).
    /// </summary>
    private async Task<bool> SendWebhookNotificationAsync(
        string webhookUrl,
        Guid fileId,
        string status,
        string details,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Sending webhook notification to {Url} for file {FileId}",
            webhookUrl,
            fileId);

        try
        {
            // Placeholder: In production, this would:
            // 1. Serialize file processing result to JSON
            // 2. POST to webhook endpoint with correlation ID header
            // 3. Handle timeout and network errors (retryable)
            // 4. Handle 4xx errors (non-retryable, log and skip)

            await Task.Delay(10, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to send webhook notification to {Url} for file {FileId}",
                webhookUrl,
                fileId);
            return false;
        }
    }

    /// <summary>
    /// Get recipient email address for a file.
    /// In production, this would query the database for user email.
    /// </summary>
    private string GetRecipientEmail(Guid fileId, string correlationId)
    {
        // Placeholder: In production, this would:
        // 1. Query File entity to get user ID
        // 2. Query User entity to get email address
        // 3. Fall back to system admin email if user email not available
        return _configuration["Notifications:DefaultRecipientEmail"] ?? "notifications@transactionprocessor.local";
    }

    /// <summary>
    /// Publish a failed notification to the notification DLQ for later retry.
    /// </summary>
    private async Task PublishToNotificationDlqAsync(
        Guid notificationId,
        Guid fileId,
        string notificationType,
        string recipientEmail,
        int attemptCount,
        string errorMessage,
        Dictionary<string, object> context,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var dlqMessage = new NotificationDlqMessage
            {
                NotificationId = notificationId,
                FileId = fileId,
                NotificationType = notificationType,
                RecipientEmail = recipientEmail,
                AttemptCount = attemptCount,
                LastAttemptAt = DateTime.UtcNow,
                ErrorMessage = errorMessage,
                Context = context
            };

            // Serialize to JSON and publish to notification-dlq
            var messageJson = JsonSerializer.Serialize(dlqMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            // In production, this would publish to notification-dlq SQS queue
            // For now, log the intent to publish
            _logger.LogInformation(
                "Publishing failed notification to notification-dlq. NotificationId: {NotificationId}, FileId: {FileId}, CorrelationId: {CorrelationId}",
                notificationId,
                fileId,
                correlationId);

            await _messageQueueService.PublishAsync(dlqMessage, correlationId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish notification to DLQ. NotificationId: {NotificationId}, FileId: {FileId}",
                notificationId,
                fileId);
        }
    }
}

/// <summary>
/// Custom exception for notification operation failures.
/// </summary>
public class NotificationException : Exception
{
    /// <summary>
    /// Initializes a new instance of NotificationException.
    /// </summary>
    public NotificationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of NotificationException with inner exception.
    /// </summary>
    public NotificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
