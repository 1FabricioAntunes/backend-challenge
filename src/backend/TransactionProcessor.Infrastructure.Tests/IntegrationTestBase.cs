using TransactionProcessor.Infrastructure.Tests.Fixtures;
using System;
using System.Threading.Tasks;
using Xunit;

namespace TransactionProcessor.Infrastructure.Tests;

/// <summary>
/// Base class for all integration tests that require database and LocalStack (S3/SQS).
/// Implements IAsyncLifetime to manage fixture lifecycle through xUnit.
/// Provides convenience methods for common test operations.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected DatabaseFixture DatabaseFixture { get; } = new();
    protected LocalStackFixture LocalStackFixture { get; } = new();

    /// <summary>
    /// Initialize both database and LocalStack fixtures.
    /// Called automatically by xUnit before each test.
    /// </summary>
    public async Task InitializeAsync()
    {
        await DatabaseFixture.InitializeAsync();
        await LocalStackFixture.InitializeAsync();
    }

    /// <summary>
    /// Dispose of both fixtures and clean up resources.
    /// Called automatically by xUnit after each test.
    /// </summary>
    public async Task DisposeAsync()
    {
        await LocalStackFixture.DisposeAsync();
        await DatabaseFixture.DisposeAsync();
    }

    /// <summary>
    /// Clear all test data to ensure test isolation.
    /// Call in your test setup if needed.
    /// </summary>
    protected async Task ClearAllTestDataAsync()
    {
        await DatabaseFixture.ClearAllDataAsync();
        await LocalStackFixture.ClearS3BucketAsync();
        await LocalStackFixture.ClearSQSQueueAsync();
    }

    /// <summary>
    /// Convenience property to access the DbContext
    /// </summary>
    protected virtual TransactionProcessor.Infrastructure.Persistence.ApplicationDbContext DbContext
        => DatabaseFixture.DbContext;

    /// <summary>
    /// Convenience property to access the S3 client
    /// </summary>
    protected virtual Amazon.S3.AmazonS3Client S3Client
        => LocalStackFixture.S3Client;

    /// <summary>
    /// Convenience property to access the SQS client
    /// </summary>
    protected virtual Amazon.SQS.AmazonSQSClient SqsClient
        => LocalStackFixture.SqsClient;

    /// <summary>
    /// Convenience property to access S3 bucket name
    /// </summary>
    protected virtual string S3BucketName
        => LocalStackFixture.S3BucketName;

    /// <summary>
    /// Convenience property to access SQS queue URL
    /// </summary>
    protected virtual string? QueueUrl
        => LocalStackFixture.QueueUrl;

    /// <summary>
    /// Convenience property to access database connection string
    /// </summary>
    protected virtual string ConnectionString
        => DatabaseFixture.ConnectionString;
}
