using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using System.Diagnostics;
using System.Text.Json;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Infrastructure.Metrics;
using TransactionProcessor.Infrastructure.Secrets;

namespace TransactionProcessor.Infrastructure.Messaging;

/// <summary>
/// SQS message queue service implementation.
/// Supports both AWS SQS and LocalStack for development.
/// Includes retry policies for transient failures with exponential backoff.
/// </summary>
public class SQSMessageQueueService : IMessageQueueService
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SQSMessageQueueService> _logger;
    private readonly string _queueUrl;
    private readonly string _dlqUrl;
    private readonly IAsyncPolicy _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the SQSMessageQueueService.
    /// </summary>
    /// <param name="sqsClient">The SQS client for AWS operations.</param>
    /// <param name="configuration">Configuration provider for queue URLs and endpoints (fallback).</param>
    /// <param name="sqsSecrets">SQS secrets loaded from Secrets Manager (primary source).</param>
    /// <param name="logger">Logger for operation tracking.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required configuration keys are missing.</exception>
    public SQSMessageQueueService(
        IAmazonSQS sqsClient, 
        IConfiguration configuration,
        TransactionProcessor.Infrastructure.Secrets.AwsSqsSecrets? sqsSecrets,
        ILogger<SQSMessageQueueService> logger)
    {
        _sqsClient = sqsClient ?? throw new ArgumentNullException(nameof(sqsClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Prefer secrets from Secrets Manager, fallback to configuration
        _queueUrl = sqsSecrets?.QueueUrl 
            ?? _configuration["AWS:SQS:QueueUrl"]
            ?? throw new InvalidOperationException("AWS:SQS:QueueUrl configuration is required");

        _dlqUrl = sqsSecrets?.DlqUrl 
            ?? _configuration["AWS:SQS:DlqUrl"] 
            ?? _configuration["AWS:SQS:DLQUrl"]  // Support both spellings
            ?? throw new InvalidOperationException("AWS:SQS:DlqUrl configuration is required");

        _retryPolicy = BuildRetryPolicy();
    }

    /// <summary>
    /// Builds the retry policy for SQS operations with exponential backoff.
    /// </summary>
    /// <returns>An async policy configured for transient SQS failures.</returns>
    private IAsyncPolicy BuildRetryPolicy()
    {
        return Policy
            .Handle<AmazonSQSException>(IsRetryableException)
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "SQS operation retry attempt {RetryCount} after {DelayMs}ms due to transient failure",
                        retryCount,
                        (int)timespan.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Determines if an SQS exception is retryable based on error code and HTTP status.
    /// Retryable errors: network errors, timeout, service unavailable, 5xx server errors.
    /// Non-retryable errors: invalid queue, access denied, authentication failure.
    /// </summary>
    /// <param name="ex">The SQS exception to evaluate.</param>
    /// <returns>True if the exception represents a transient error; otherwise, false.</returns>
    private static bool IsRetryableException(AmazonSQSException ex)
    {
        // Non-retryable errors: client errors that won't succeed on retry
        if (ex.ErrorCode == "InvalidParameterValue" || ex.ErrorCode == "QueueDoesNotExist") return false;
        if (ex.ErrorCode == "NotFound") return false;
        if (ex.ErrorCode == "AccessDenied") return false;
        if (ex.ErrorCode == "InvalidSignature" || ex.ErrorCode == "AuthFailure") return false;

        // Retryable errors: service unavailable, throttling, request timeout
        if (ex.ErrorCode == "ServiceUnavailable" || ex.ErrorCode == "ServiceDown") return true;
        if (ex.ErrorCode == "RequestLimitExceeded" || ex.ErrorCode == "ThrottlingException") return true;
        if (ex.ErrorCode == "RequestTimeout" || ex.ErrorCode == "OperationAborted") return true;
        if ((int?)ex.StatusCode >= 500) return true;  // Server errors (5xx)
        if (ex.StatusCode == System.Net.HttpStatusCode.RequestTimeout) return true;

        // Network-related errors are retryable
        if (ex.InnerException is HttpRequestException || ex.InnerException is TimeoutException)
            return true;

        return false;
    }

    /// <summary>
    /// Publishes a message to the SQS queue.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <typeparam name="T">The type of the message body</typeparam>
    /// <param name="message">The message to publish</param>
    /// <param name="correlationId">Correlation ID for tracking and tracing</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>The message ID assigned by SQS</returns>
    /// <remarks>
    /// Message attributes stored: CorrelationId, Timestamp, MessageType
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// Non-retryable: queue not found, access denied, authentication failure
    /// </remarks>
    public async Task<string> PublishAsync<T>(T message, string correlationId, CancellationToken cancellationToken = default)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation ID cannot be empty", nameof(correlationId));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var messageBody = JsonSerializer.Serialize(message);

            _logger.LogInformation(
                "Publishing message to SQS: CorrelationId={CorrelationId}, QueueUrl={QueueUrl}",
                correlationId, _queueUrl);

            string messageId = string.Empty;

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var sendMessageRequest = new SendMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MessageBody = messageBody,
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        {
                            "CorrelationId", new MessageAttributeValue
                            {
                                StringValue = correlationId,
                                DataType = "String"
                            }
                        },
                        {
                            "Timestamp", new MessageAttributeValue
                            {
                                StringValue = DateTime.UtcNow.ToString("O"),
                                DataType = "String"
                            }
                        },
                        {
                            "MessageType", new MessageAttributeValue
                            {
                                StringValue = typeof(T).Name,
                                DataType = "String"
                            }
                        }
                    }
                };

                var response = await _sqsClient.SendMessageAsync(sendMessageRequest, cancellationToken);
                messageId = response.MessageId;

                _logger.LogInformation(
                    "Message published successfully to SQS: MessageId={MessageId}, CorrelationId={CorrelationId}",
                    messageId, correlationId);
            });

            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("publish").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordSqsMessageProcessed("main_queue", "success");
            return messageId;
        }
        catch (AmazonSQSException ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("publish").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_publish_error");
            _logger.LogError(ex, "AWS SQS error during message publish: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new MessageQueueException($"Failed to publish message to SQS: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("publish").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_publish_unhandled");
            _logger.LogError(ex, "Unexpected error during message publish: CorrelationId={CorrelationId}", correlationId);
            throw new MessageQueueException($"Unexpected error during message publish: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Receives messages from the SQS queue.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <typeparam name="T">The type of the message body</typeparam>
    /// <param name="maxNumberOfMessages">Maximum number of messages to receive (1-10, default: 10)</param>
    /// <param name="visibilityTimeoutSeconds">Visibility timeout in seconds (default: 300 = 5 minutes)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>List of received messages; empty list if no messages available</returns>
    /// <remarks>
    /// Visibility timeout: How long message is hidden from other consumers after receipt
    /// Max messages: 10 is AWS SQS limit for single request
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// </remarks>
    public async Task<IEnumerable<QueueMessage<T>>> ReceiveMessagesAsync<T>(
        int maxNumberOfMessages = 10,
        int visibilityTimeoutSeconds = 300,
        CancellationToken cancellationToken = default)
    {
        if (maxNumberOfMessages < 1 || maxNumberOfMessages > 10)
            throw new ArgumentException("Max messages must be between 1 and 10", nameof(maxNumberOfMessages));
        if (visibilityTimeoutSeconds < 0 || visibilityTimeoutSeconds > 43200)
            throw new ArgumentException("Visibility timeout must be between 0 and 43200 seconds", nameof(visibilityTimeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Receiving messages from SQS: MaxMessages={MaxMessages}, VisibilityTimeout={VisibilityTimeout}s",
                maxNumberOfMessages, visibilityTimeoutSeconds);

            var messages = new List<QueueMessage<T>>();

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var receiveMessageRequest = new ReceiveMessageRequest
                {
                    QueueUrl = _queueUrl,
                    MaxNumberOfMessages = maxNumberOfMessages,
                    VisibilityTimeout = visibilityTimeoutSeconds,
                    MessageAttributeNames = new List<string> { "All" },
                    WaitTimeSeconds = 20  // Long polling for efficiency
                };

                var response = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest, cancellationToken);

                foreach (var message in response.Messages)
                {
                    try
                    {
                        var body = JsonSerializer.Deserialize<T>(message.Body);
                        if (body == null) continue;

                        var queueMessage = new QueueMessage<T>
                        {
                            MessageId = message.MessageId,
                            Body = body,
                            ReceiptHandle = message.ReceiptHandle,
                            Attributes = message.MessageAttributes?.ToDictionary(
                                kvp => kvp.Key,
                                kvp => kvp.Value?.StringValue ?? string.Empty) ?? new()
                        };

                        messages.Add(queueMessage);

                        _logger.LogInformation("Message received from SQS: MessageId={MessageId}, MessageType={MessageType}",
                            message.MessageId,
                            message.MessageAttributes?.FirstOrDefault(x => x.Key == "MessageType").Value?.StringValue ?? "Unknown");
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize SQS message body: MessageId={MessageId}", message.MessageId);
                    }
                }
            });

            if (messages.Count == 0)
            {
                _logger.LogDebug("No messages received from SQS queue");
            }

            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("receive").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.UpdateQueueDepth("main_queue", messages.Count);
            return messages;
        }
        catch (AmazonSQSException ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("receive").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_receive_error");
            _logger.LogError(ex, "AWS SQS error during message receive: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new MessageQueueException($"Failed to receive messages from SQS: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("receive").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_receive_unhandled");
            _logger.LogError(ex, "Unexpected error during message receive");
            throw new MessageQueueException($"Unexpected error during message receive: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deletes a message from the SQS queue after successful processing.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <param name="receiptHandle">The receipt handle of the message to delete</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <remarks>
    /// Receipt handle: Unique identifier provided when message is received
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// </remarks>
    public async Task DeleteMessageAsync(string receiptHandle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptHandle))
            throw new ArgumentException("Receipt handle cannot be empty", nameof(receiptHandle));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Deleting message from SQS: ReceiptHandle={ReceiptHandle}", receiptHandle);

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var deleteMessageRequest = new DeleteMessageRequest
                {
                    QueueUrl = _queueUrl,
                    ReceiptHandle = receiptHandle
                };

                await _sqsClient.DeleteMessageAsync(deleteMessageRequest, cancellationToken);
            });

            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("delete").Observe(stopwatch.Elapsed.TotalSeconds);
            _logger.LogInformation("Message deleted successfully from SQS");
        }
        catch (AmazonSQSException ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("delete").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_delete_error");
            _logger.LogError(ex, "AWS SQS error during message deletion: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new MessageQueueException($"Failed to delete message from SQS: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("delete").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_delete_unhandled");
            _logger.LogError(ex, "Unexpected error during message deletion");
            throw new MessageQueueException($"Unexpected error during message deletion: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets messages from the Dead Letter Queue for monitoring and debugging.
    /// Includes automatic retry on transient failures (exponential backoff, max 3 retries).
    /// </summary>
    /// <typeparam name="T">The type of the message body</typeparam>
    /// <param name="maxNumberOfMessages">Maximum number of messages to receive (1-10)</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>List of messages from the DLQ</returns>
    /// <remarks>
    /// DLQ messages: Messages that failed processing after max receive count
    /// Monitoring: Use for debugging and investigating failed messages
    /// Retries: exponential backoff 2s, 4s, 8s for transient failures
    /// </remarks>
    public async Task<IEnumerable<QueueMessage<T>>> GetDLQMessagesAsync<T>(
        int maxNumberOfMessages = 10,
        CancellationToken cancellationToken = default)
    {
        if (maxNumberOfMessages < 1 || maxNumberOfMessages > 10)
            throw new ArgumentException("Max messages must be between 1 and 10", nameof(maxNumberOfMessages));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Retrieving messages from DLQ: MaxMessages={MaxMessages}", maxNumberOfMessages);

            var dlqMessages = new List<QueueMessage<T>>();

            // Execute with retry policy for transient failures
            await _retryPolicy.ExecuteAsync(async () =>
            {
                var receiveMessageRequest = new ReceiveMessageRequest
                {
                    QueueUrl = _dlqUrl,
                    MaxNumberOfMessages = maxNumberOfMessages,
                    MessageAttributeNames = new List<string> { "All" }
                };

                var response = await _sqsClient.ReceiveMessageAsync(receiveMessageRequest, cancellationToken);

                foreach (var message in response.Messages)
                {
                    try
                    {
                        var body = JsonSerializer.Deserialize<T>(message.Body);
                        if (body == null) continue;

                        var queueMessage = new QueueMessage<T>
                        {
                            MessageId = message.MessageId,
                            Body = body,
                            ReceiptHandle = message.ReceiptHandle,
                            Attributes = message.MessageAttributes?.ToDictionary(
                                kvp => kvp.Key,
                                kvp => kvp.Value?.StringValue ?? string.Empty) ?? new()
                        };

                        dlqMessages.Add(queueMessage);

                        _logger.LogWarning("DLQ message found: MessageId={MessageId}", message.MessageId);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize DLQ message body: MessageId={MessageId}", message.MessageId);
                    }
                }
            });

            if (dlqMessages.Count == 0)
            {
                _logger.LogInformation("No messages found in DLQ");
            }

            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("dlq_receive").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.UpdateDlqDepth("main_queue", dlqMessages.Count);
            return dlqMessages;
        }
        catch (AmazonSQSException ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("dlq_receive").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_dlq_receive_error");
            _logger.LogError(ex, "AWS SQS error retrieving DLQ messages: ErrorCode={ErrorCode}, Message={Message}", ex.ErrorCode, ex.Message);
            throw new MessageQueueException($"Failed to retrieve DLQ messages from SQS: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MetricsService.SQSOperationDurationSeconds.WithLabels("dlq_receive").Observe(stopwatch.Elapsed.TotalSeconds);
            MetricsService.RecordError("sqs_dlq_receive_unhandled");
            _logger.LogError(ex, "Unexpected error retrieving DLQ messages");
            throw new MessageQueueException($"Unexpected error retrieving DLQ messages: {ex.Message}", ex);
        }
    }
}
