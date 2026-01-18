using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransactionProcessor.Domain.Interfaces;

namespace TransactionProcessor.Worker;

/// <summary>
/// Background worker that polls the notification DLQ and retries failed notifications.
/// - Polls every 60s
/// - Uses INotificationService.RetryFailedNotificationAsync (which applies 2s/4s/8s backoff)
/// - Deletes DLQ message on success or after retry failure (manual review)
/// - Tracks success/failure counters per cycle
/// </summary>
public class NotificationDlqWorker : BackgroundService
{
    private readonly IAmazonSQS _sqs;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationDlqWorker> _logger;
    private readonly INotificationService _notificationService;

    private string _notificationDlqUrl = string.Empty;

    public NotificationDlqWorker(
        IAmazonSQS sqs,
        IConfiguration configuration,
        ILogger<NotificationDlqWorker> logger,
        INotificationService notificationService)
    {
        _sqs = sqs ?? throw new ArgumentNullException(nameof(sqs));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _notificationDlqUrl = _configuration["AWS:SQS:NotificationDlqUrl"]
            ?? _configuration["AWS:SQS:DLQUrl"]
            ?? throw new InvalidOperationException("Notification DLQ URL not configured (AWS:SQS:NotificationDlqUrl or AWS:SQS:DLQUrl)");

        _logger.LogInformation("Notification DLQ worker started. QueueUrl={QueueUrl}", _notificationDlqUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            int successCount = 0;
            int failureCount = 0;

            try
            {
                // Long-poll DLQ (wait up to 20s)
                var receiveRequest = new ReceiveMessageRequest
                {
                    QueueUrl = _notificationDlqUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20,
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqs.ReceiveMessageAsync(receiveRequest, stoppingToken);

                if (response.Messages.Count == 0)
                {
                    _logger.LogDebug("Notification DLQ is empty.");
                }

                foreach (var msg in response.Messages)
                {
                    string correlationId = msg.MessageAttributes?.GetValueOrDefault("CorrelationId")?.StringValue
                        ?? $"dlq-retry:{msg.MessageId}";

                    try
                    {
                        var payload = JsonSerializer.Deserialize<NotificationDlqPayload>(msg.Body);
                        if (payload == null)
                        {
                            _logger.LogWarning("Skipping DLQ message due to null payload. MessageId={MessageId}", msg.MessageId);
                            await DeleteMessageAsync(msg.ReceiptHandle, stoppingToken);
                            continue;
                        }

                        _logger.LogInformation(
                            "Retrying notification from DLQ. NotificationId={NotificationId}, FileId={FileId}, Type={NotificationType}, CorrelationId={CorrelationId}",
                            payload.NotificationId,
                            payload.FileId,
                            payload.NotificationType,
                            correlationId);

                        var result = await _notificationService.RetryFailedNotificationAsync(
                            payload.NotificationId,
                            correlationId,
                            stoppingToken);

                        if (result.Success)
                        {
                            successCount++;
                            await DeleteMessageAsync(msg.ReceiptHandle, stoppingToken);
                            _logger.LogInformation(
                                "Notification retry succeeded. NotificationId={NotificationId}. DLQ message deleted.",
                                payload.NotificationId);
                        }
                        else
                        {
                            failureCount++;
                            // After service-level retries (2s,4s,8s) failed, delete DLQ message and log for manual review
                            await DeleteMessageAsync(msg.ReceiptHandle, stoppingToken);
                            _logger.LogError(
                                "Notification retry failed. NotificationId={NotificationId}, Attempts={Attempts}, Reason={Reason}. DLQ message deleted for manual review.",
                                result.NotificationId,
                                result.AttemptCount,
                                result.FailureReason);
                        }
                    }
                    catch (JsonException ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, "Failed to deserialize notification DLQ payload. MessageId={MessageId}. Deleting message.", msg.MessageId);
                        await DeleteMessageAsync(msg.ReceiptHandle, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        _logger.LogError(ex, "Unexpected error during DLQ retry. MessageId={MessageId}. Deleting message to avoid poison loop.", msg.MessageId);
                        await DeleteMessageAsync(msg.ReceiptHandle, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while polling notification DLQ");
            }

            _logger.LogInformation("Notification DLQ cycle summary: Success={SuccessCount}, Failure={FailureCount}", successCount, failureCount);

            // Poll every 60 seconds
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Notification DLQ worker stopping.");
    }

    private async Task DeleteMessageAsync(string receiptHandle, CancellationToken ct)
    {
        await _sqs.DeleteMessageAsync(new DeleteMessageRequest
        {
            QueueUrl = _notificationDlqUrl,
            ReceiptHandle = receiptHandle
        }, ct);
    }

    /// <summary>
    /// DTO for DLQ payloads published by NotificationService.
    /// Matches the documented format in IMPLEMENTATION_GUIDE.md.
    /// </summary>
    private sealed class NotificationDlqPayload
    {
        public Guid NotificationId { get; set; }
        public Guid FileId { get; set; }
        public string NotificationType { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public DateTime LastAttemptAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}