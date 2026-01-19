using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace TransactionProcessor.Infrastructure.Secrets;

/// <summary>
/// Service for retrieving and caching secrets from AWS Secrets Manager.
/// Supports both LocalStack (development) and AWS Secrets Manager (production).
/// </summary>
public class SecretsManagerService
{
    private readonly IAmazonSecretsManager _client;
    private readonly ILogger<SecretsManagerService> _logger;
    private readonly ConcurrentDictionary<string, string> _secretCache;

    /// <summary>
    /// Initializes a new instance of the SecretsManagerService.
    /// </summary>
    /// <param name="configuration">Configuration for LocalStack endpoint and region</param>
    /// <param name="logger">Logger for secret retrieval events</param>
    public SecretsManagerService(IConfiguration configuration, ILogger<SecretsManagerService> logger)
    {
        _logger = logger;
        _secretCache = new ConcurrentDictionary<string, string>();

        // Configure AWS SDK for LocalStack (development) or AWS (production)
        var region = configuration["AWS:Region"] ?? "us-east-1";
        var serviceUrl = configuration["AWS:SecretsManager:ServiceUrl"];

        if (!string.IsNullOrEmpty(serviceUrl))
        {
            // LocalStack configuration (development)
            _logger.LogInformation("Configuring Secrets Manager with LocalStack endpoint: {Endpoint}", serviceUrl);
            var credentials = new Amazon.Runtime.BasicAWSCredentials("test", "test");
            _client = new AmazonSecretsManagerClient(
                credentials,
                new AmazonSecretsManagerConfig
                {
                    ServiceURL = serviceUrl,
                    AuthenticationRegion = region
                });
        }
        else
        {
            // AWS Secrets Manager configuration (production)
            _logger.LogInformation("Configuring Secrets Manager with AWS region: {Region}", region);
            _client = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.GetBySystemName(region));
        }
    }

    /// <summary>
    /// Retrieves and caches a secret from AWS Secrets Manager.
    /// Secrets are cached in memory for performance.
    /// </summary>
    /// <typeparam name="T">Type to deserialize the secret JSON into</typeparam>
    /// <param name="secretId">Secret identifier (hierarchical naming: AppName/Component/Secret)</param>
    /// <param name="correlationId">Correlation ID for logging and tracing</param>
    /// <returns>Deserialized secret object</returns>
    /// <exception cref="InvalidOperationException">Thrown when secret retrieval fails</exception>
    public async Task<T> GetSecretAsync<T>(string secretId, string? correlationId = null)
        where T : class
    {
        if (string.IsNullOrEmpty(secretId))
        {
            throw new ArgumentException("Secret ID cannot be null or empty", nameof(secretId));
        }

        // Check cache first
        if (_secretCache.TryGetValue(secretId, out var cachedSecret))
        {
            _logger.LogDebug(
                "Secret retrieved from cache. SecretId={SecretId}, CorrelationId={CorrelationId}",
                secretId,
                correlationId);

            return JsonSerializer.Deserialize<T>(cachedSecret)
                ?? throw new InvalidOperationException($"Failed to deserialize cached secret: {secretId}");
        }

        try
        {
            _logger.LogInformation(
                "Retrieving secret from Secrets Manager. SecretId={SecretId}, CorrelationId={CorrelationId}",
                secretId,
                correlationId);

            var request = new GetSecretValueRequest { SecretId = secretId };
            var response = await _client.GetSecretValueAsync(request);

            // Cache the secret
            var secretValue = response.SecretString;
            _secretCache.AddOrUpdate(secretId, secretValue, (_, _) => secretValue);

            _logger.LogInformation(
                "Secret retrieved and cached successfully. SecretId={SecretId}, CorrelationId={CorrelationId}",
                secretId,
                correlationId);

            return JsonSerializer.Deserialize<T>(secretValue)
                ?? throw new InvalidOperationException($"Failed to deserialize secret: {secretId}");
        }
        catch (ResourceNotFoundException ex)
        {
            _logger.LogError(
                ex,
                "Secret not found in Secrets Manager. SecretId={SecretId}, CorrelationId={CorrelationId}",
                secretId,
                correlationId);

            throw new InvalidOperationException(
                $"Secret '{secretId}' not found. Ensure the secret is created in AWS Secrets Manager.",
                ex);
        }
        catch (AmazonSecretsManagerException ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve secret from Secrets Manager. SecretId={SecretId}, CorrelationId={CorrelationId}, Error={Error}",
                secretId,
                correlationId,
                ex.Message);

            throw new InvalidOperationException(
                $"Failed to retrieve secret '{secretId}': {ex.Message}",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error retrieving secret. SecretId={SecretId}, CorrelationId={CorrelationId}",
                secretId,
                correlationId);

            throw new InvalidOperationException(
                $"Unexpected error retrieving secret '{secretId}': {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Clears the secret cache. Useful for testing or manual cache invalidation.
    /// </summary>
    public void ClearCache()
    {
        _secretCache.Clear();
        _logger.LogInformation("Secret cache cleared");
    }

    /// <summary>
    /// Clears a specific secret from the cache.
    /// </summary>
    /// <param name="secretId">Secret identifier to remove from cache</param>
    public void ClearCacheEntry(string secretId)
    {
        if (_secretCache.TryRemove(secretId, out _))
        {
            _logger.LogInformation("Cache entry cleared for secret: {SecretId}", secretId);
        }
    }
}
