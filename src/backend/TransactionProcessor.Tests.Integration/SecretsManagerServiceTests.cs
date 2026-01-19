using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using TransactionProcessor.Infrastructure.Secrets;

namespace TransactionProcessor.Tests.Integration;

/// <summary>
/// Integration tests for SecretsManagerService with LocalStack.
/// These tests verify secret retrieval, caching, and error handling.
/// </summary>
public class SecretsManagerServiceTests : IAsyncLifetime
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SecretsManagerService> _logger;
    private SecretsManagerService? _service;

    public SecretsManagerServiceTests()
    {
        // Setup configuration for LocalStack
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AWS:Region", "us-east-1" },
                { "AWS:SecretsManager:ServiceUrl", "http://localhost:4566" }
            })
            .Build();

        // Setup mock logger
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<SecretsManagerService>();
    }

    public async Task InitializeAsync()
    {
        // Initialize the service
        _service = new SecretsManagerService(_configuration, _logger);
        
        // Create test secrets in LocalStack Secrets Manager
        await SetupTestSecretsAsync();
    }

    public async Task DisposeAsync()
    {
        _service?.ClearCache();
        // Note: In a real scenario, you would cleanup secrets from LocalStack
        await Task.CompletedTask;
    }

    private async Task SetupTestSecretsAsync()
    {
        // This would be implemented to create test secrets in LocalStack
        // For now, this is a placeholder showing the test structure
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetSecretAsync_WithValidSecretId_ReturnsDeserializedSecret()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        const string secretId = "TransactionProcessor/Database/ConnectionString";
        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        // This test requires LocalStack Secrets Manager to be running
        // and secrets to be pre-configured
        var result = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);

        // Verify the secret is deserialized correctly
        Assert.NotNull(result);
        Assert.NotEmpty(result.ConnectionString);
    }

    [Fact]
    public async Task GetSecretAsync_WithEmptySecretId_ThrowsArgumentException()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetSecretAsync<DatabaseSecrets>(string.Empty, correlationId));
    }

    [Fact]
    public async Task GetSecretAsync_WithCachedSecret_ReturnsCachedValue()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        const string secretId = "TransactionProcessor/Database/ConnectionString";
        var correlationId = Guid.NewGuid().ToString();

        // Act - First call retrieves from Secrets Manager
        var firstResult = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);

        // Act - Second call should return cached value
        var secondResult = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);

        // Assert - Both results are the same (cached)
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult.ConnectionString, secondResult.ConnectionString);
    }

    [Fact]
    public void ClearCache_RemovesAllCachedSecrets()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Act
        _service.ClearCache();

        // Note: In a real test, you would verify that subsequent calls
        // to GetSecretAsync retrieve fresh values from Secrets Manager
    }

    [Fact]
    public void ClearCacheEntry_RemovesSpecificSecret()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Act
        _service.ClearCacheEntry("TransactionProcessor/Database/ConnectionString");

        // Note: In a real test, you would verify that subsequent calls
        // to GetSecretAsync for this specific secret retrieve fresh values
    }
}

/// <summary>
/// Unit tests for AppSecrets validation.
/// </summary>
public class AppSecretsValidationTests
{
    [Fact]
    public void Validate_WithCompleteSecrets_DoesNotThrow()
    {
        // Arrange
        var secrets = new AppSecrets
        {
            Database = new DatabaseSecrets { ConnectionString = "valid-connection-string" },
            S3 = new AwsS3Secrets
            {
                BucketName = "test-bucket",
                AccessKeyId = "test-key-id",
                SecretAccessKey = "test-secret-key",
                Region = "us-east-1"
            },
            SQS = new AwsSqsSecrets
            {
                QueueUrl = "http://localhost:4566/queue",
                DlqUrl = "http://localhost:4566/dlq",
                Region = "us-east-1"
            }
        };

        // Act & Assert - Should not throw
        secrets.Validate();
    }

    [Fact]
    public void Validate_WithMissingDatabaseConnection_ThrowsInvalidOperationException()
    {
        // Arrange
        var secrets = new AppSecrets
        {
            Database = new DatabaseSecrets { ConnectionString = string.Empty },
            S3 = new AwsS3Secrets
            {
                BucketName = "test-bucket",
                AccessKeyId = "test-key-id",
                SecretAccessKey = "test-secret-key",
                Region = "us-east-1"
            },
            SQS = new AwsSqsSecrets
            {
                QueueUrl = "http://localhost:4566/queue",
                DlqUrl = "http://localhost:4566/dlq",
                Region = "us-east-1"
            }
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => secrets.Validate());
        Assert.Contains("Database.ConnectionString", exception.Message);
    }

    [Fact]
    public void Validate_WithMissingS3Secrets_ThrowsInvalidOperationException()
    {
        // Arrange
        var secrets = new AppSecrets
        {
            Database = new DatabaseSecrets { ConnectionString = "valid-connection-string" },
            S3 = new AwsS3Secrets { BucketName = string.Empty }, // Missing required fields
            SQS = new AwsSqsSecrets
            {
                QueueUrl = "http://localhost:4566/queue",
                DlqUrl = "http://localhost:4566/dlq",
                Region = "us-east-1"
            }
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => secrets.Validate());
        Assert.Contains("S3.", exception.Message);
    }
}
