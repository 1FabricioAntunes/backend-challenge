using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionProcessor.Application.DTOs;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Application.Services;

namespace TransactionProcessor.Worker;

/// <summary>
/// Background service that consumes file processing messages from SQS.
/// Polls queue for messages, deserializes them, and orchestrates processing.
/// Handles graceful shutdown and backoff on empty queue.
/// </summary>
public class FileProcessingHostedService : BackgroundService
{
    private readonly IMessageQueueService _queueService;
    private readonly IFileProcessingService _fileProcessingService;
    private readonly ILogger<FileProcessingHostedService> _logger;

    /// <summary>
    /// Delay in milliseconds when queue is empty (backoff).
    /// Prevents excessive polling when no messages available.
    /// </summary>
    private const int EmptyQueueDelayMs = 5000; // 5 seconds

    /// <summary>
    /// Delay in milliseconds between batches when processing completes.
    /// </summary>
    private const int ProcessingDelayMs = 100;

    /// <summary>
    /// Maximum number of messages to receive per batch.
    /// SQS limit is 10.
    /// </summary>
    private const int MaxMessagesPerBatch = 10;

    /// <summary>
    /// Visibility timeout for messages in seconds.
    /// Must align with SQS queue configuration (typically 5 minutes).
    /// </summary>
    private const int VisibilityTimeoutSeconds = 300;

    public FileProcessingHostedService(
        IMessageQueueService queueService,
        IFileProcessingService fileProcessingService,
        ILogger<FileProcessingHostedService> logger)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _fileProcessingService = fileProcessingService ?? throw new ArgumentNullException(nameof(fileProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the background service polling loop.
    /// Continues until cancellation is requested (graceful shutdown).
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File processing service starting");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Processing cancelled, initiating graceful shutdown");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in polling loop, continuing");
                    await Task.Delay(EmptyQueueDelayMs, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Service shutdown initiated");
        }
        finally
        {
            _logger.LogInformation("File processing service stopped");
        }
    }

    /// <summary>
    /// Processes a single batch of messages from the queue.
    /// </summary>
    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        // Receive batch of messages
        var messages = await _queueService.ReceiveMessagesAsync<FileProcessingMessageDto>(
            maxNumberOfMessages: MaxMessagesPerBatch,
            visibilityTimeoutSeconds: VisibilityTimeoutSeconds,
            cancellationToken: cancellationToken);

        var messageList = messages.ToList();
        if (messageList.Count == 0)
        {
            _logger.LogDebug("No messages in queue, waiting before next poll");
            await Task.Delay(EmptyQueueDelayMs, cancellationToken);
            return;
        }

        _logger.LogInformation("Received {MessageCount} messages from queue", messageList.Count);

        // Process each message
        foreach (var message in messageList)
        {
            var shouldDeleteMessage = false;
            
            try
            {
                shouldDeleteMessage = await ProcessMessageAsync(message, cancellationToken);
                
                // Delete message if processing succeeded or validation failed
                if (shouldDeleteMessage)
                {
                    await _queueService.DeleteMessageAsync(message.ReceiptHandle, cancellationToken);
                    _logger.LogInformation("Message deleted from queue: {MessageId}", message.MessageId);
                }
                else
                {
                    _logger.LogWarning("Message not deleted, will be retried: {MessageId}", message.MessageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {MessageId}, message will be retried", message.MessageId);
                // Message will be redelivered after visibility timeout expires
            }

            // Small delay between processing messages
            await Task.Delay(ProcessingDelayMs, cancellationToken);
        }

        _logger.LogInformation("Batch processing completed, processed {MessageCount} messages", messageList.Count);
    }

    /// <summary>
    /// Processes a single message.
    /// Extracts file processing details and orchestrates file processing.
    /// </summary>
    /// <returns>True if message should be deleted (success or validation error), False if should retry</returns>
    private async Task<bool> ProcessMessageAsync(
        TransactionProcessor.Domain.Interfaces.QueueMessage<FileProcessingMessageDto> message,
        CancellationToken cancellationToken)
    {
        if (message?.Body == null)
        {
            _logger.LogWarning("Received message with null body: {MessageId}", message?.MessageId);
            return true; // Delete invalid messages
        }

        var msgData = message.Body;
        var correlationId = message.Attributes.TryGetValue("CorrelationId", out var corId) 
            ? corId 
            : Guid.NewGuid().ToString();

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            { "CorrelationId", correlationId },
            { "MessageId", message.MessageId },
            { "FileId", msgData.FileId },
            { "FileName", msgData.FileName }
        });

        try
        {
            _logger.LogInformation(
                "Processing file: {FileName} (ID: {FileId}) from S3: {S3Key}",
                msgData.FileName,
                msgData.FileId,
                msgData.S3Key);

            // Orchestrate file processing
            var result = await _fileProcessingService.ProcessFileAsync(
                fileId: msgData.FileId,
                s3Key: msgData.S3Key,
                fileName: msgData.FileName,
                correlationId: correlationId,
                cancellationToken: cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "File processing succeeded: {StoresUpserted} stores, {TransactionsInserted} transactions",
                    result.StoresUpserted,
                    result.TransactionsInserted);
                return true; // Delete message on success
            }
            else
            {
                _logger.LogError(
                    "File processing failed: {ErrorMessage}. Validation errors: {@ValidationErrors}",
                    result.ErrorMessage,
                    result.ValidationErrors);
                
                // For validation errors, delete the message (not retryable)
                if (result.ValidationErrors.Count > 0)
                {
                    _logger.LogInformation("Validation error - message will be deleted and not retried");
                    return true; // Delete message - validation errors shouldn't retry
                }

                // Processing error without validation errors - shouldn't reach here due to exception
                _logger.LogWarning("Processing error without exception thrown - leaving message for retry");
                return false; // Don't delete - let it retry
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception processing file {FileName}. Message will be retried",
                msgData.FileName);
            return false; // Don't delete - processing error should retry
        }
    }

    /// <summary>
    /// Called when service is stopping (graceful shutdown).
    /// Allows time for in-flight messages to complete.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("File processing service stopping gracefully");
        await base.StopAsync(cancellationToken);
    }
}
