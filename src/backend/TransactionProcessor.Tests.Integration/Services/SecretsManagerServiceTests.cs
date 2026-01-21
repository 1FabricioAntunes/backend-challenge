using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Testcontainers.LocalStack;
using TransactionProcessor.Infrastructure.Secrets;
using Xunit;

namespace TransactionProcessor.Tests.Integration.Services;

/// <summary>
/// Integration tests for SecretsManagerService with LocalStack Secrets Manager
/// Tests secret retrieval, caching, and concurrent access patterns using Testcontainers
/// 
/// NOTE: These tests require LocalStack to be available via Testcontainers.
/// They may fail if Docker is not running or LocalStack image is not available.
/// </summary>
[Trait("Category", "RequiresLocalStack")]
public class SecretsManagerServiceTests : IAsyncLifetime
{
    private LocalStackContainer? _localStackContainer;
    private IAmazonSecretsManager? _secretsManagerClient;
    private IConfiguration? _configuration;
    private SecretsManagerService? _service;
    private ILogger<SecretsManagerService>? _logger;

    public async Task InitializeAsync()
    {
        // Create and start LocalStack container with Secrets Manager service
        _localStackContainer = new LocalStackBuilder()
            .WithImage("localstack/localstack:latest")
            .WithEnvironment("SERVICES", "secretsmanager")
            .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
            .Build();

        await _localStackContainer.StartAsync();

        // Configure AWS SDK to use LocalStack endpoint
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AWS:Region", "us-east-1" },
                { "AWS:SecretsManager:ServiceUrl", "http://localhost:4566" }
            })
            .Build();

        _configuration = configuration;

        // Create Secrets Manager client pointing to LocalStack
        var secretsManagerConfig = new AmazonSecretsManagerConfig
        {
            ServiceURL = "http://localhost:4566"
        };

        _secretsManagerClient = new AmazonSecretsManagerClient("test", "test", secretsManagerConfig);

        // Create logger factory
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder.AddConsole());
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _logger = serviceProvider.GetRequiredService<ILogger<SecretsManagerService>>();

        // Initialize secrets in LocalStack
        await InitializeSecretsAsync();

        // Create the service to test
        _service = new SecretsManagerService(_configuration, _logger);
    }

    public async Task DisposeAsync()
    {
        if (_localStackContainer != null)
        {
            await _localStackContainer.StopAsync();
            await _localStackContainer.DisposeAsync();
        }

        _secretsManagerClient?.Dispose();
    }

    private async Task InitializeSecretsAsync()
    {
        if (_secretsManagerClient == null)
            throw new InvalidOperationException("Secrets Manager client not initialized");

        // Database connection string
        await CreateSecretAsync(
            "TransactionProcessor/Database/ConnectionString",
            "Host=localhost;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres");

        // S3 secrets as JSON
        var s3Secrets = new
        {
            BucketName = "cnab-files",
            AccessKeyId = "test",
            SecretAccessKey = "test",
            Region = "us-east-1"
        };
        await CreateSecretAsync(
            "TransactionProcessor/AWS/S3",
            JsonSerializer.Serialize(s3Secrets));

        // SQS secrets as JSON
        var sqsSecrets = new
        {
            QueueUrl = "http://localhost:4566/000000000000/file-processing-queue",
            DlqUrl = "http://localhost:4566/000000000000/file-processing-dlq",
            Region = "us-east-1"
        };
        await CreateSecretAsync(
            "TransactionProcessor/AWS/SQS",
            JsonSerializer.Serialize(sqsSecrets));

        // OAuth secrets as JSON
        var oauthSecrets = new
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            Authority = "http://localhost:4566/cognito",
            Audience = "transaction-processor-api"
        };
        await CreateSecretAsync(
            "TransactionProcessor/OAuth",
            JsonSerializer.Serialize(oauthSecrets));
    }

    private async Task CreateSecretAsync(string secretName, string secretValue)
    {
        if (_secretsManagerClient == null)
            throw new InvalidOperationException("Secrets Manager client not initialized");

        try
        {
            await _secretsManagerClient.CreateSecretAsync(new CreateSecretRequest
            {
                Name = secretName,
                SecretString = secretValue,
                Tags = new List<Tag>
                {
                    new Tag { Key = "Application", Value = "TransactionProcessor" },
                    new Tag { Key = "Environment", Value = "Development" }
                }
            });
        }
        catch (ResourceExistsException)
        {
            // Secret already exists, update it instead
            await _secretsManagerClient.UpdateSecretAsync(new UpdateSecretRequest
            {
                SecretId = secretName,
                SecretString = secretValue
            });
        }
    }


    /// <summary>
    /// Test: Retrieve existing secret returns correct value
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task GetSecretAsync_WithValidSecretId_ReturnsDeserializedSecret()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        const string secretId = "TransactionProcessor/Database/ConnectionString";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var result = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("localhost", result.ConnectionString);
        Assert.Contains("transactionprocessor", result.ConnectionString);
    }

    /// <summary>
    /// Test: Retrieve S3 secrets as JSON object
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task GetSecretAsync_WithJsonSecret_ReturnsDeserializedObject()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        const string secretId = "TransactionProcessor/AWS/S3";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var secret = await _service.GetSecretAsync<AwsS3Secrets>(secretId, correlationId);

        // Assert
        Assert.NotNull(secret);
        Assert.Equal("cnab-files", secret.BucketName);
        Assert.Equal("test", secret.AccessKeyId);
        Assert.Equal("test", secret.SecretAccessKey);
        Assert.Equal("us-east-1", secret.Region);
    }

    /// <summary>
    /// Test: Retrieve non-existent secret throws exception
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task GetSecretAsync_WithNonExistentSecretId_ThrowsException()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        const string nonExistentSecretId = "TransactionProcessor/NonExistent/Secret";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.GetSecretAsync<DatabaseSecrets>(nonExistentSecretId, correlationId));
    }

    /// <summary>
    /// Test: Secrets cached after first retrieval
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task GetSecretAsync_AfterFirstRetrieval_UsesCachedValue()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        const string secretId = "TransactionProcessor/Database/ConnectionString";
        var correlationId = Guid.NewGuid().ToString();

        // Act - First retrieval
        var firstSecret = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);

        // Act - Second retrieval (should use cache)
        var secondSecret = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);

        // Assert - Both should return the same values
        Assert.NotNull(firstSecret);
        Assert.NotNull(secondSecret);
        Assert.Equal(firstSecret.ConnectionString, secondSecret.ConnectionString);
    }

    /// <summary>
    /// Test: Multiple concurrent retrievals use cache
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task GetSecretAsync_WithConcurrentRequests_AllUseCache()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        const string secretId = "TransactionProcessor/AWS/S3";
        const int numberOfConcurrentRequests = 10;
        var tasks = new List<Task<AwsS3Secrets>>();

        // Prime the cache with one request
        await _service.GetSecretAsync<AwsS3Secrets>(secretId, correlationId);

        // Act - Make multiple concurrent requests for the same secret
        for (int i = 0; i < numberOfConcurrentRequests; i++)
        {
            tasks.Add(_service.GetSecretAsync<AwsS3Secrets>(secretId, correlationId));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be identical (same cached value)
        Assert.NotEmpty(results);
        var firstResult = results[0];

        foreach (var result in results)
        {
            Assert.Equal(firstResult.BucketName, result.BucketName);
            Assert.Equal(firstResult.AccessKeyId, result.AccessKeyId);
            Assert.Equal(firstResult.SecretAccessKey, result.SecretAccessKey);
            Assert.Equal(firstResult.Region, result.Region);
        }
    }

    /// <summary>
    /// Test: Different secret types can be retrieved
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task GetSecretAsync_WithDifferentSecretTypes_ReturnsCorrectTypes()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act & Assert - Database Secrets
        var dbSecret = await _service.GetSecretAsync<DatabaseSecrets>(
            "TransactionProcessor/Database/ConnectionString",
            correlationId);
        Assert.NotNull(dbSecret);
        Assert.NotEmpty(dbSecret.ConnectionString);

        // Act & Assert - S3 Secrets
        var s3Secret = await _service.GetSecretAsync<AwsS3Secrets>(
            "TransactionProcessor/AWS/S3",
            correlationId);
        Assert.NotNull(s3Secret);
        Assert.Equal("cnab-files", s3Secret.BucketName);

        // Act & Assert - SQS Secrets
        var sqsSecret = await _service.GetSecretAsync<AwsSqsSecrets>(
            "TransactionProcessor/AWS/SQS",
            correlationId);
        Assert.NotNull(sqsSecret);
        Assert.Contains("file-processing-queue", sqsSecret.QueueUrl);

        // Act & Assert - OAuth Secrets
        var oauthSecret = await _service.GetSecretAsync<OAuthSecrets>(
            "TransactionProcessor/OAuth",
            correlationId);
        Assert.NotNull(oauthSecret);
        Assert.Equal("test-client-id", oauthSecret.ClientId);
    }

    /// <summary>
    /// Test: Cache can be cleared for specific secret
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task ClearCacheEntry_RemovesSecretFromCache()
    {
        if (_service == null || _secretsManagerClient == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        const string secretId = "TransactionProcessor/Database/ConnectionString";

        // Prime the cache
        var firstRetrieve = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);
        Assert.NotNull(firstRetrieve);
        var originalConnection = firstRetrieve.ConnectionString;

        // Act - Clear the cache entry
        _service.ClearCacheEntry(secretId);

        // Update the secret in LocalStack
        await _secretsManagerClient.UpdateSecretAsync(new UpdateSecretRequest
        {
            SecretId = secretId,
            SecretString = "Host=updated-host;Port=5432;Database=transactionprocessor;Username=postgres;Password=postgres"
        });

        // Retrieve again (should fetch from Secrets Manager, not cache)
        var secondRetrieve = await _service.GetSecretAsync<DatabaseSecrets>(secretId, correlationId);

        // Assert - Should get updated value
        Assert.NotNull(secondRetrieve);
        Assert.Contains("updated-host", secondRetrieve.ConnectionString);
        Assert.NotEqual(originalConnection, secondRetrieve.ConnectionString);
    }

    /// <summary>
    /// Test: All cache can be cleared
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task ClearCache_RemovesAllSecretsFromCache()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Prime the cache with multiple secrets
        await _service.GetSecretAsync<DatabaseSecrets>(
            "TransactionProcessor/Database/ConnectionString",
            correlationId);
        await _service.GetSecretAsync<AwsS3Secrets>(
            "TransactionProcessor/AWS/S3",
            correlationId);
        await _service.GetSecretAsync<AwsSqsSecrets>(
            "TransactionProcessor/AWS/SQS",
            correlationId);

        // Act - Clear all cache
        _service.ClearCache();

        // Verify cache is cleared by checking that we can still retrieve (no exceptions)
        var dbSecret = await _service.GetSecretAsync<DatabaseSecrets>(
            "TransactionProcessor/Database/ConnectionString",
            correlationId);
        Assert.NotNull(dbSecret);

        var s3Secret = await _service.GetSecretAsync<AwsS3Secrets>(
            "TransactionProcessor/AWS/S3",
            correlationId);
        Assert.NotNull(s3Secret);
    }

    /// <summary>
    /// Test: Secrets integration with AppSecrets validation
    /// </summary>
    [Fact(Skip = "Requires LocalStack container running on localhost:4566")]
    public async Task AppSecrets_WithLoadedSecrets_ValidatesSuccessfully()
    {
        if (_service == null)
            throw new InvalidOperationException("Service not initialized");

        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var appSecrets = new AppSecrets();

        // Act - Load secrets into AppSecrets
        appSecrets.Database = await _service.GetSecretAsync<DatabaseSecrets>(
            "TransactionProcessor/Database/ConnectionString",
            correlationId);
        appSecrets.S3 = await _service.GetSecretAsync<AwsS3Secrets>(
            "TransactionProcessor/AWS/S3",
            correlationId);
        appSecrets.SQS = await _service.GetSecretAsync<AwsSqsSecrets>(
            "TransactionProcessor/AWS/SQS",
            correlationId);
        appSecrets.OAuth = await _service.GetSecretAsync<OAuthSecrets>(
            "TransactionProcessor/OAuth",
            correlationId);

        // Assert - Validation should not throw
        appSecrets.Validate(); // Should not throw InvalidOperationException
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
