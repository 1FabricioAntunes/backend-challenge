using Amazon.S3;
using Amazon.SQS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Testcontainers.LocalStack;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests.Fixtures;

/// <summary>
/// LocalStack fixture that manages AWS services emulation (S3, SQS) for testing.
/// Implements IAsyncLifetime to support async setup/teardown in xUnit.
/// Creates S3 bucket and SQS queue for each test run.
/// </summary>
public class LocalStackFixture : IAsyncLifetime
{
    private LocalStackContainer? _container;
    private AmazonS3Client? _s3Client;
    private AmazonSQSClient? _sqsClient;

    /// <summary>
    /// S3 bucket name used in tests
    /// </summary>
    private const string BucketName = "test-cnab-bucket";

    /// <summary>
    /// SQS queue name used in tests
    /// </summary>
    private const string QueueName = "test-transaction-queue";

    /// <summary>
    /// Gets the S3 client for bucket operations.
    /// Available after InitializeAsync() is called.
    /// </summary>
    public AmazonS3Client S3Client
    {
        get => _s3Client ?? throw new InvalidOperationException("S3Client not initialized");
    }

    /// <summary>
    /// Gets the SQS client for queue operations.
    /// Available after InitializeAsync() is called.
    /// </summary>
    public AmazonSQSClient SqsClient
    {
        get => _sqsClient ?? throw new InvalidOperationException("SqsClient not initialized");
    }

    /// <summary>
    /// Gets the S3 bucket name for tests
    /// </summary>
    public string S3BucketName => BucketName;

    /// <summary>
    /// Gets the SQS queue URL for tests
    /// </summary>
    public string? QueueUrl { get; private set; }

    /// <summary>
    /// Gets the LocalStack container endpoint
    /// </summary>
    public string? LocalStackEndpoint { get; private set; }

    /// <summary>
    /// Initialize the LocalStack container and create S3/SQS resources.
    /// Called automatically by xUnit before test execution.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create and start LocalStack container with S3 and SQS services
        _container = new LocalStackBuilder()
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        // Get LocalStack endpoint
        LocalStackEndpoint = _container.GetConnectionString();

        // Create S3 client configured for LocalStack
        _s3Client = new AmazonS3Client(
            new Amazon.Runtime.BasicAWSCredentials("test", "test"),
            new AmazonS3Config
            {
                ServiceURL = LocalStackEndpoint,
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true,
                UseHttp = true
            });

        // Create SQS client configured for LocalStack
        _sqsClient = new AmazonSQSClient(
            new Amazon.Runtime.BasicAWSCredentials("test", "test"),
            new AmazonSQSConfig
            {
                ServiceURL = LocalStackEndpoint,
                AuthenticationRegion = "us-east-1",
                UseHttp = true
            });

        // Create S3 bucket
        await _s3Client.PutBucketAsync(BucketName);

        // Create SQS queue and get its URL
        var queueResponse = await _sqsClient.CreateQueueAsync(QueueName);
        QueueUrl = queueResponse.QueueUrl;
    }

    /// <summary>
    /// Clean up and stop the LocalStack container.
    /// Called automatically by xUnit after test execution.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_s3Client is not null)
        {
            _s3Client.Dispose();
        }

        if (_sqsClient is not null)
        {
            _sqsClient.Dispose();
        }

        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Clears all objects from the S3 bucket.
    /// Useful for test isolation between tests.
    /// </summary>
    public async Task ClearS3BucketAsync()
    {
        var listRequest = new Amazon.S3.Model.ListObjectsV2Request
        {
            BucketName = BucketName
        };
        var objects = await _s3Client?.ListObjectsV2Async(listRequest)!;
        if (objects?.S3Objects?.Count > 0)
        {
            var deleteRequest = new Amazon.S3.Model.DeleteObjectsRequest
            {
                BucketName = BucketName,
                Objects = objects.S3Objects
                    .Select(obj => new Amazon.S3.Model.KeyVersion { Key = obj.Key })
                    .ToList()
            };

            await _s3Client?.DeleteObjectsAsync(deleteRequest)!;
        }
    }

    /// <summary>
    /// Clears all messages from the SQS queue.
    /// Useful for test isolation between tests.
    /// </summary>
    public async Task ClearSQSQueueAsync()
    {
        if (string.IsNullOrEmpty(QueueUrl) || _sqsClient is null)
        {
            return;
        }

        // Purge the queue to remove all messages
        await _sqsClient.PurgeQueueAsync(QueueUrl);
    }

    /// <summary>
    /// Gets the approximate number of messages in the SQS queue.
    /// </summary>
    /// <returns>Approximate message count</returns>
    public async Task<int> GetQueueMessageCountAsync()
    {
        if (string.IsNullOrEmpty(QueueUrl) || _sqsClient is null)
        {
            return 0;
        }

        var attributes = await _sqsClient.GetQueueAttributesAsync(
            new Amazon.SQS.Model.GetQueueAttributesRequest
            {
                QueueUrl = QueueUrl,
                AttributeNames = new List<string> { "ApproximateNumberOfMessages" }
            });

        if (attributes?.Attributes?.TryGetValue("ApproximateNumberOfMessages", out var count) ?? false)
        {
            return int.Parse(count);
        }

        return 0;
    }
}
