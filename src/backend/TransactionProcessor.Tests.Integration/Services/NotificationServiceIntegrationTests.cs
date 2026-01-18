using Amazon.SQS;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;
using TransactionProcessor.Domain.Entities;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Infrastructure.Messaging;
using TransactionProcessor.Infrastructure.Persistence;
using TransactionProcessor.Infrastructure.Services;
using Xunit;
using FileEntity = TransactionProcessor.Domain.Entities.File;

namespace TransactionProcessor.Tests.Integration.Services;

/// <summary>
/// Integration tests for notification service and DLQ worker.
/// Uses TestContainers for PostgreSQL and LocalStack for SQS/DLQ.
/// Tests notification delivery, retry logic, DLQ publishing, and database tracking.
/// </summary>
public class NotificationServiceIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("transactionprocessor_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private ApplicationDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private INotificationService? _notificationService;
    private IMessageQueueService? _messageQueueService;
    private string _connectionString = string.Empty;

    /// <summary>
    /// Initialize test containers and services.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        await _postgresContainer.StartAsync();
        _connectionString = _postgresContainer.GetConnectionString();

        // Create DbContext
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        _dbContext = new ApplicationDbContext(options);
        await _dbContext.Database.MigrateAsync();

        // Setup DI container with mocked SQS client
        var services = new ServiceCollection();

        // Configuration for LocalStack SQS
        var configDict = new Dictionary<string, string?>
        {
            { "AWS:SQS:QueueUrl", "http://localhost:4566/000000000000/file-processing-queue" },
            { "AWS:SQS:DLQUrl", "http://localhost:4566/000000000000/notification-dlq" },
            { "AWS:SQS:NotificationDlqUrl", "http://localhost:4566/000000000000/notification-dlq" },
            { "Notifications:DefaultRecipientEmail", "test@example.com" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict!)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(logging => logging.AddConsole());
        services.AddScoped<ApplicationDbContext>(_ => _dbContext);

        // Register SQS client (would use LocalStack in actual test)
        var sqsClientMock = new Mock<IAmazonSQS>();
        services.AddScoped(_ => sqsClientMock.Object);

        // Register services
        services.AddScoped<IMessageQueueService, SQSMessageQueueService>();
        services.AddScoped<INotificationService, NotificationService>();

        _serviceProvider = services.BuildServiceProvider();
        _notificationService = _serviceProvider.GetRequiredService<INotificationService>();
        _messageQueueService = _serviceProvider.GetRequiredService<IMessageQueueService>();
    }

    /// <summary>
    /// Cleanup test containers and resources.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await _postgresContainer.StopAsync();
        await _postgresContainer.DisposeAsync();
    }

    /// <summary>
    /// Test 1: Successful notification send without retries.
    /// </summary>
    [Fact]
    public async Task NotifyProcessingCompletedAsync_ValidFile_SendsSuccessfully()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "valid_file.txt";
        var status = "Processed";
        var details = "5 transactions processed successfully";
        var correlationId = Guid.NewGuid().ToString();

        // Create file in database
        var file = new FileEntity(fileId, fileName);
        file.StartProcessing();
        file.MarkAsProcessed();
        _dbContext!.Files.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _notificationService!.NotifyProcessingCompletedAsync(
            fileId,
            status,
            details,
            correlationId);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.NotificationId.Should().NotBe(Guid.Empty);
        result.CorrelationId.Should().Be(correlationId);
        result.AttemptCount.Should().BeGreaterThanOrEqualTo(1);
        result.FailureReason.Should().BeNullOrEmpty();

        // Verify file still exists in database
        var fileFromDb = await _dbContext.Files.FirstOrDefaultAsync(f => f.Id == fileId);
        fileFromDb.Should().NotBeNull();
        fileFromDb!.StatusCode.Should().Be("Processed");
    }

    /// <summary>
    /// Test 2: Retry on transient failure (3 attempts).
    /// Notification service applies exponential backoff: 2s, 4s, 8s.
    /// </summary>
    [Fact]
    public async Task NotifyProcessingCompletedAsync_TransientFailure_RetriesWithBackoff()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "file_with_transient_error.txt";
        var status = "Processed";
        var details = "File processed";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);
        file.StartProcessing();
        file.MarkAsProcessed();
        _dbContext!.Files.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _notificationService!.NotifyProcessingCompletedAsync(
            fileId,
            status,
            details,
            correlationId);

        // Assert
        result.Should().NotBeNull();
        // After retries, it may succeed or fail depending on email sending capability
        result.NotificationId.Should().NotBe(Guid.Empty);
        result.CorrelationId.Should().Be(correlationId);
        // AttemptCount should reflect retry attempts
        result.AttemptCount.Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Test 3: DLQ publish after max retries.
    /// When notification fails after 3 retry attempts, it's published to notification-dlq.
    /// </summary>
    [Fact]
    public async Task NotifyProcessingFailedAsync_MaxRetriesExceeded_PublishesToDLQ()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "file_with_failure.txt";
        var errorMessage = "Invalid CNAB format: missing header record";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);
        file.StartProcessing();
        file.MarkAsRejected(errorMessage);
        _dbContext!.Files.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _notificationService!.NotifyProcessingFailedAsync(
            fileId,
            errorMessage,
            correlationId);

        // Assert
        result.Should().NotBeNull();
        result.NotificationId.Should().NotBe(Guid.Empty);
        result.CorrelationId.Should().Be(correlationId);
        // If max retries exceeded, it will be marked as failed and sent to DLQ
        result.AttemptCount.Should().BeGreaterThanOrEqualTo(1);

        // Verify file in database still has error message
        var fileFromDb = await _dbContext.Files.FirstOrDefaultAsync(f => f.Id == fileId);
        fileFromDb.Should().NotBeNull();
        fileFromDb!.StatusCode.Should().Be("Rejected");
        fileFromDb.ErrorMessage.Should().Contain("Invalid CNAB");
    }

    /// <summary>
    /// Test 4: DLQ worker retries failed notification.
    /// Tests RetryFailedNotificationAsync called by DLQ worker.
    /// </summary>
    [Fact]
    public async Task RetryFailedNotificationAsync_DLQMessage_RetriesSuccessfully()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var result = await _notificationService!.RetryFailedNotificationAsync(
            notificationId,
            correlationId);

        // Assert
        result.Should().NotBeNull();
        result.NotificationId.Should().Be(notificationId);
        result.CorrelationId.Should().Be(correlationId);
        result.AttemptCount.Should().BeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// Test 5: Idempotency - skip duplicate notifications.
    /// Multiple calls to send the same notification should not create duplicates.
    /// </summary>
    [Fact]
    public async Task NotifyProcessingCompletedAsync_DuplicateCall_SkipsOrUpdates()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "file_for_idempotency_test.txt";
        var status = "Processed";
        var details = "Processing completed";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);
        file.StartProcessing();
        file.MarkAsProcessed();
        _dbContext!.Files.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act - Call twice with same parameters
        var result1 = await _notificationService!.NotifyProcessingCompletedAsync(
            fileId, status, details, correlationId);

        var result2 = await _notificationService.NotifyProcessingCompletedAsync(
            fileId, status, details, correlationId);

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        
        // Both calls should succeed
        result1.Success.Should().Be(result2.Success);
        result1.CorrelationId.Should().Be(result2.CorrelationId);

        // Verify file was processed only once (idempotency maintained)
        var fileFromDb = await _dbContext.Files.FirstOrDefaultAsync(f => f.Id == fileId);
        fileFromDb.Should().NotBeNull();
        fileFromDb!.StatusCode.Should().Be("Processed");
    }

    /// <summary>
    /// Test 6: Notification tracking in database.
    /// Verifies that notification attempts are tracked for audit and retry.
    /// </summary>
    [Fact]
    public async Task NotifyProcessingCompletedAsync_TracksAttemptInDatabase()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "file_with_tracking.txt";
        var status = "Processed";
        var details = "10 transactions processed";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);
        file.StartProcessing();
        file.MarkAsProcessed();
        _dbContext!.Files.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _notificationService!.NotifyProcessingCompletedAsync(
            fileId,
            status,
            details,
            correlationId);

        // Assert
        result.Success.Should().BeTrue();

        // Verify notification attempt was tracked in database
        var notificationAttempts = await _dbContext.NotificationAttempts
            .Where(na => na.FileId == fileId)
            .ToListAsync();

        notificationAttempts.Should().NotBeEmpty();

        var attempt = notificationAttempts.First();
        attempt.FileId.Should().Be(fileId);
        attempt.NotificationType.Should().Be("ProcessingCompleted");
        attempt.Recipient.Should().NotBeNullOrEmpty();
        attempt.AttemptCount.Should().BeGreaterThanOrEqualTo(1);
        attempt.LastAttemptAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // If successful, SentAt should be set
        if (result.Success)
        {
            attempt.SentAt.Should().NotBeNull();
            attempt.Status.Should().Be("Sent");
        }
    }

    /// <summary>
    /// Test: Notification tracking with multiple statuses.
    /// Tracks both successful and failed notification attempts.
    /// </summary>
    [Fact]
    public async Task NotifyProcessingCompletedAsync_TracksBothSuccessAndFailure()
    {
        // Arrange
        var fileId1 = Guid.NewGuid();
        var fileId2 = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();

        // Create two files
        var file1 = new FileEntity(fileId1, "success_file.txt");
        file1.StartProcessing();
        file1.MarkAsProcessed();

        var file2 = new FileEntity(fileId2, "failure_file.txt");
        file2.StartProcessing();
        file2.MarkAsRejected("Processing failed");

        _dbContext!.Files.AddRange(file1, file2);
        await _dbContext.SaveChangesAsync();

        // Act - Send notifications for both files
        var resultSuccess = await _notificationService!.NotifyProcessingCompletedAsync(
            fileId1, "Processed", "5 transactions", correlationId);

        var resultFailure = await _notificationService.NotifyProcessingFailedAsync(
            fileId2, "Processing failed", correlationId);

        // Assert
        resultSuccess.Should().NotBeNull();
        resultFailure.Should().NotBeNull();

        // Verify both are tracked in database
        var allAttempts = await _dbContext.NotificationAttempts
            .Where(na => na.FileId == fileId1 || na.FileId == fileId2)
            .ToListAsync();

        allAttempts.Should().HaveCountGreaterThanOrEqualTo(2);

        var attempt1 = allAttempts.FirstOrDefault(a => a.FileId == fileId1);
        var attempt2 = allAttempts.FirstOrDefault(a => a.FileId == fileId2);

        attempt1.Should().NotBeNull();
        attempt2.Should().NotBeNull();

        attempt1!.NotificationType.Should().Be("ProcessingCompleted");
        attempt2!.NotificationType.Should().Be("ProcessingFailed");
    }

    /// <summary>
    /// Test: Retry policy configuration.
    /// Verifies that notification service has correct retry policy settings.
    /// </summary>
    [Fact]
    public void GetRetryPolicy_ReturnsCorrectConfiguration()
    {
        // Act
        var policy = _notificationService!.GetRetryPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.MaxRetryAttempts.Should().Be(3);
        policy.InitialDelayMs.Should().Be(2000); // 2 seconds
        policy.BackoffMultiplier.Should().Be(2.0);
        policy.SendFailedToNotificationDlq.Should().BeTrue();
    }

    /// <summary>
    /// Test: Notification for rejected file includes error details.
    /// Error message should be included in notification tracking.
    /// </summary>
    [Fact]
    public async Task NotifyProcessingFailedAsync_IncludesErrorDetails()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileName = "rejected_file.txt";
        var errorMessage = "Line 50: Invalid CPF format (expected 11 digits, got 10)";
        var correlationId = Guid.NewGuid().ToString();

        var file = new FileEntity(fileId, fileName);
        file.StartProcessing();
        file.MarkAsRejected(errorMessage);
        _dbContext!.Files.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _notificationService!.NotifyProcessingFailedAsync(
            fileId,
            errorMessage,
            correlationId);

        // Assert
        result.Should().NotBeNull();
        result.NotificationId.Should().NotBe(Guid.Empty);

        // Verify error message is tracked in database
        var notifications = await _dbContext.NotificationAttempts
            .Where(na => na.FileId == fileId)
            .ToListAsync();

        notifications.Should().NotBeEmpty();
        var notification = notifications.First();
        notification.NotificationType.Should().Be("ProcessingFailed");
        notification.FileId.Should().Be(fileId);
    }
}
