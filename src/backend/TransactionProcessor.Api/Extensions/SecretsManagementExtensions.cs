using Microsoft.Extensions.Logging;
using Serilog;
using TransactionProcessor.Infrastructure.Secrets;

namespace TransactionProcessor.Api.Extensions;

/// <summary>
/// Extension methods for secrets management configuration
/// </summary>
public static class SecretsManagementExtensions
{
    /// <summary>
    /// Adds secrets management with AWS Secrets Manager integration
    /// Loads secrets at startup, validates them, and registers in DI container
    /// Supports both LocalStack (development) and AWS Secrets Manager (production)
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="correlationId">Correlation ID for request tracking</param>
    /// <returns>Loaded and validated AppSecrets instance</returns>
    public static AppSecrets AddSecretsManagementAndGetSecrets(
        this IServiceCollection services,
        IConfiguration configuration,
        string correlationId)
    {
        Log.Information("[{CorrelationId}] Loading secrets from AWS Secrets Manager...", correlationId);

        try
        {
            // Create logger for SecretsManagerService
            var loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
            var logger = loggerFactory.CreateLogger<SecretsManagerService>();
            
            // Register SecretsManagerService as singleton
            var secretsManagerService = new SecretsManagerService(configuration, logger);
            
            services.AddSingleton(secretsManagerService);

            // Load all application secrets at startup (fail-fast pattern)
            var appSecrets = LoadSecretsAsync(secretsManagerService, configuration, correlationId).GetAwaiter().GetResult();

            // Validate that all required secrets are present
            appSecrets.Validate();

            // Register secrets in DI container for injection into services
            services.AddSingleton(appSecrets);
            services.AddSingleton(appSecrets.Database);
            services.AddSingleton(appSecrets.S3);
            services.AddSingleton(appSecrets.SQS);
            
            // OAuth is optional (not required in development)
            if (appSecrets.OAuth != null)
            {
                services.AddSingleton(appSecrets.OAuth);
            }

            Log.Information("[{CorrelationId}] Successfully loaded and validated all required secrets", correlationId);
            Log.Information("[{CorrelationId}] - Database secrets: Loaded", correlationId);
            Log.Information("[{CorrelationId}] - S3 secrets: Loaded (Bucket: {BucketName}, Region: {Region})", 
                correlationId, appSecrets.S3.BucketName, appSecrets.S3.Region);
            Log.Information("[{CorrelationId}] - SQS secrets: Loaded (Region: {Region})", 
                correlationId, appSecrets.SQS.Region);
            
            if (appSecrets.OAuth != null)
            {
                Log.Information("[{CorrelationId}] - OAuth secrets: Loaded (Authority: {Authority})", 
                    correlationId, appSecrets.OAuth.Authority);
            }
            else
            {
                Log.Information("[{CorrelationId}] - OAuth secrets: Skipped (not configured for development)", 
                    correlationId);
            }

            return appSecrets;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[{CorrelationId}] Failed to load secrets from AWS Secrets Manager. " +
                "Application cannot start without required secrets. Error: {ErrorMessage}", 
                correlationId, ex.Message);
            throw new InvalidOperationException(
                "Failed to load required secrets from AWS Secrets Manager. " +
                "Ensure all secrets are configured in LocalStack or AWS Secrets Manager. " +
                $"Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads all application secrets from Secrets Manager with fallback to appsettings
    /// </summary>
    private static async Task<AppSecrets> LoadSecretsAsync(
        SecretsManagerService secretsManager,
        IConfiguration configuration,
        string correlationId)
    {
        var appSecrets = new AppSecrets();

        // Load Database secrets
        appSecrets.Database = await LoadSecretWithFallbackAsync<DatabaseSecrets>(
            secretsManager,
            configuration,
            "TransactionProcessor/Database/ConnectionString",
            "ConnectionStrings:Default",
            correlationId,
            connectionString => new DatabaseSecrets { ConnectionString = connectionString });

        // Load S3 secrets
        appSecrets.S3 = await LoadS3SecretsAsync(secretsManager, configuration, correlationId);

        // Load SQS secrets
        appSecrets.SQS = await LoadSqsSecretsAsync(secretsManager, configuration, correlationId);

        // Load OAuth secrets (optional in development)
        try
        {
            appSecrets.OAuth = await LoadOAuthSecretsAsync(secretsManager, configuration, correlationId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{CorrelationId}] OAuth secrets not found - skipping (optional in development)", 
                correlationId);
        }

        return appSecrets;
    }

    /// <summary>
    /// Loads a secret with fallback to appsettings configuration
    /// </summary>
    private static async Task<T> LoadSecretWithFallbackAsync<T>(
        SecretsManagerService secretsManager,
        IConfiguration configuration,
        string secretId,
        string fallbackConfigKey,
        string correlationId,
        Func<string, T> createFromString) where T : class
    {
        try
        {
            // Try to load from Secrets Manager first
            return await secretsManager.GetSecretAsync<T>(secretId, correlationId);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{CorrelationId}] Failed to load secret {SecretId} from Secrets Manager, " +
                "falling back to appsettings (development convenience only)", correlationId, secretId);

            // SECURITY: Fallback to appsettings is for development convenience only
            // Production deployments MUST use Secrets Manager - fail fast if not available
            // See: docs/security.md § Secrets Management
            // See: technical-decisions.md § 3.1 Configuration-First Principle
            var isDevelopment = configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    $"SECURITY: Secret {secretId} MUST be retrieved from Secrets Manager in production. " +
                    $"Fallback to appsettings is not allowed. Error: {ex.Message}", ex);
            }

            // Fallback to appsettings (development only)
            var configValue = configuration[fallbackConfigKey];
            if (string.IsNullOrWhiteSpace(configValue))
            {
                throw new InvalidOperationException(
                    $"Secret {secretId} not found in Secrets Manager and fallback configuration key " +
                    $"{fallbackConfigKey} is not set in appsettings. " +
                    $"Ensure LocalStack Secrets Manager is running and secret is initialized. " +
                    $"See: src/infra/localstack-init/init-secrets.sh", ex);
            }

            Log.Warning("[{CorrelationId}] Using fallback connection string from appsettings (development only). " +
                "Production deployments must use Secrets Manager.", correlationId);
            return createFromString(configValue);
        }
    }

    /// <summary>
    /// Loads S3 secrets from Secrets Manager or appsettings
    /// </summary>
    private static async Task<AwsS3Secrets> LoadS3SecretsAsync(
        SecretsManagerService secretsManager,
        IConfiguration configuration,
        string correlationId)
    {
        try
        {
            return await secretsManager.GetSecretAsync<AwsS3Secrets>(
                "TransactionProcessor/AWS/S3", correlationId);
        }
        catch
        {
            Log.Warning("[{CorrelationId}] S3 secrets not found in Secrets Manager, using appsettings", 
                correlationId);

            return new AwsS3Secrets
            {
                BucketName = configuration["AWS:S3:BucketName"] ?? "cnab-files",
                AccessKeyId = configuration["AWS:S3:AccessKeyId"] ?? "test",
                SecretAccessKey = configuration["AWS:S3:SecretAccessKey"] ?? "test",
                Region = configuration["AWS:Region"] ?? "us-east-1"
            };
        }
    }

    /// <summary>
    /// Loads SQS secrets from Secrets Manager or appsettings
    /// </summary>
    private static async Task<AwsSqsSecrets> LoadSqsSecretsAsync(
        SecretsManagerService secretsManager,
        IConfiguration configuration,
        string correlationId)
    {
        try
        {
            return await secretsManager.GetSecretAsync<AwsSqsSecrets>(
                "TransactionProcessor/AWS/SQS", correlationId);
        }
        catch
        {
            Log.Warning("[{CorrelationId}] SQS secrets not found in Secrets Manager, using appsettings", 
                correlationId);

            return new AwsSqsSecrets
            {
                QueueUrl = configuration["AWS:SQS:QueueUrl"] 
                    ?? "http://localhost:4566/000000000000/file-processing-queue",
                DlqUrl = configuration["AWS:SQS:DlqUrl"] 
                    ?? "http://localhost:4566/000000000000/file-processing-dlq",
                Region = configuration["AWS:Region"] ?? "us-east-1"
            };
        }
    }

    /// <summary>
    /// Loads OAuth secrets from Secrets Manager or appsettings
    /// </summary>
    private static async Task<OAuthSecrets> LoadOAuthSecretsAsync(
        SecretsManagerService secretsManager,
        IConfiguration configuration,
        string correlationId)
    {
        try
        {
            return await secretsManager.GetSecretAsync<OAuthSecrets>(
                "TransactionProcessor/OAuth", correlationId);
        }
        catch
        {
            // OAuth is optional in development, check if configured in appsettings
            var authority = configuration["Authentication:Authority"];
            if (string.IsNullOrWhiteSpace(authority))
            {
                throw new InvalidOperationException("OAuth secrets not configured");
            }

            Log.Warning("[{CorrelationId}] OAuth secrets not found in Secrets Manager, using appsettings", 
                correlationId);

            return new OAuthSecrets
            {
                ClientId = configuration["Authentication:ClientId"] ?? string.Empty,
                ClientSecret = configuration["Authentication:ClientSecret"] ?? string.Empty,
                Authority = authority,
                Audience = configuration["Authentication:Audience"] ?? string.Empty
            };
        }
    }
}
