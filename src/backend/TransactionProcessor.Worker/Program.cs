using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionProcessor.Application.Services;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Messaging;
using TransactionProcessor.Infrastructure.Persistence;
using TransactionProcessor.Infrastructure.Repositories;
using TransactionProcessor.Infrastructure.Secrets;
using TransactionProcessor.Infrastructure.Services;
using TransactionProcessor.Infrastructure.Storage;
using TransactionProcessor.Worker;

// Build host
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.SetBasePath(Directory.GetCurrentDirectory());
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddDebug();

        if (context.HostingEnvironment.IsDevelopment())
        {
            logging.SetMinimumLevel(LogLevel.Debug);
        }
        else
        {
            logging.SetMinimumLevel(LogLevel.Information);
        }
    })
    .ConfigureServices((context, services) =>
    {
        // ============================================================================
        // SECRETS MANAGER CONFIGURATION
        // ============================================================================
        // Load secrets from AWS Secrets Manager (LocalStack in dev, AWS in prod)
        var startupCorrelationId = Guid.NewGuid().ToString();
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().AddDebug());
        var logger = loggerFactory.CreateLogger<SecretsManagerService>();
        var secretsManagerService = new SecretsManagerService(context.Configuration, logger);
        services.AddSingleton(secretsManagerService);

        // Load secrets synchronously (simplified version for worker)
        var appSecrets = SecretsHelper.LoadSecretsAsync(secretsManagerService, context.Configuration, startupCorrelationId).GetAwaiter().GetResult();
        appSecrets.Validate();
        
        // Register secrets in DI container
        services.AddSingleton(appSecrets);
        services.AddSingleton(appSecrets.Database);
        services.AddSingleton(appSecrets.S3);
        services.AddSingleton(appSecrets.SQS);

        // ============================================================================
        // DATABASE CONFIGURATION
        // ============================================================================
        // Get connection string from loaded secrets (already validated)
        var connectionString = appSecrets.Database.ConnectionString;
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsqlOptions => npgsqlOptions.CommandTimeout(60)));

        // ============================================================================
        // AWS CLIENT CONFIGURATION
        // ============================================================================
        // Register AWS S3 client (supports LocalStack for development)
        var s3ServiceUrl = context.Configuration["AWS:S3:ServiceUrl"] 
            ?? context.Configuration["AWS:S3:ServiceURL"]
            ?? context.Configuration["AWS__S3__ServiceURL"];
        var sqsServiceUrl = context.Configuration["AWS:SQS:ServiceUrl"] 
            ?? context.Configuration["AWS:SQS:ServiceURL"]
            ?? context.Configuration["AWS__SQS__ServiceURL"];
        var awsRegion = context.Configuration["AWS:Region"] ?? "us-east-1";

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion)
            };
            
            if (!string.IsNullOrEmpty(s3ServiceUrl))
            {
                config.ServiceURL = s3ServiceUrl;
                config.ForcePathStyle = true;
                var credentials = new BasicAWSCredentials("test", "test");
                return new AmazonS3Client(credentials, config);
            }
            
            return new AmazonS3Client(config);
        });

        // Register AWS SQS client (supports LocalStack for development)
        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var config = new AmazonSQSConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion)
            };
            
            if (!string.IsNullOrEmpty(sqsServiceUrl))
            {
                config.ServiceURL = sqsServiceUrl;
                var credentials = new BasicAWSCredentials("test", "test");
                return new AmazonSQSClient(credentials, config);
            }
            
            return new AmazonSQSClient(config);
        });

        // ============================================================================
        // REPOSITORIES
        // ============================================================================
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        // ============================================================================
        // APPLICATION SERVICES
        // ============================================================================
        services.AddScoped<ICNABParser, CNABParser>();
        services.AddScoped<ICNABValidator, CNABValidator>();
        services.AddScoped<IFileProcessingService, FileProcessingService>();

        // ============================================================================
        // INFRASTRUCTURE SERVICES
        // ============================================================================
        services.AddScoped<IFileStorageService, S3FileStorageService>();
        
        // Register SQS Message Queue Service with explicit AwsSqsSecrets injection
        services.AddScoped<IMessageQueueService>(sp =>
        {
            var sqsClient = sp.GetRequiredService<IAmazonSQS>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var sqsSecrets = sp.GetService<AwsSqsSecrets>();
            var logger = sp.GetRequiredService<ILogger<SQSMessageQueueService>>();
            return new SQSMessageQueueService(sqsClient, configuration, sqsSecrets, logger);
        });

        // ============================================================================
        // NOTIFICATION SERVICE
        // ============================================================================
        services.AddScoped<INotificationService, NotificationService>();

        // ============================================================================
        // HOSTED SERVICES
        // ============================================================================
        services.AddHostedService<FileProcessingHostedService>();
        services.AddHostedService<NotificationDlqWorker>();

        // ============================================================================
        // CONFIGURATION
        // ============================================================================
        services.Configure<FileProcessingOptions>(
            context.Configuration.GetSection("FileProcessing"));
    })
    .Build();

// Run migrations on startup (development only)
if (host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
{
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.Migrate();
        host.Services.GetRequiredService<ILogger<Program>>()
            .LogInformation("Database migrations applied");
    }
}

await host.RunAsync();

namespace TransactionProcessor.Worker
{
    /// <summary>
    /// Helper class for loading secrets
    /// </summary>
    internal static class SecretsHelper
    {
        /// <summary>
        /// Helper method to load secrets from Secrets Manager
        /// </summary>
        internal static async Task<AppSecrets> LoadSecretsAsync(
            SecretsManagerService secretsManager,
            IConfiguration configuration,
            string correlationId)
        {
            var appSecrets = new AppSecrets();
            var isDevelopment = configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            var maxRetries = isDevelopment ? 5 : 1;
            var retryDelay = TimeSpan.FromSeconds(2);
            Exception? lastException = null;

            // Load Database connection string
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var connectionStringSecret = await secretsManager.GetSecretStringAsync(
                        "TransactionProcessor/Database/ConnectionString", correlationId);
                    appSecrets.Database = new DatabaseSecrets { ConnectionString = connectionStringSecret };
                    break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(retryDelay);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(appSecrets.Database.ConnectionString))
            {
                if (!isDevelopment)
                {
                    throw new InvalidOperationException(
                        $"Database connection string MUST be retrieved from Secrets Manager in production. " +
                        $"Error: {lastException?.Message}", lastException);
                }
                var configValue = configuration["ConnectionStrings:DefaultConnection"];
                if (string.IsNullOrWhiteSpace(configValue))
                {
                    throw new InvalidOperationException(
                        $"Database connection string not found in Secrets Manager and fallback is not set. " +
                        $"Error: {lastException?.Message}", lastException);
                }
                appSecrets.Database = new DatabaseSecrets { ConnectionString = configValue };
            }

            // Load S3 secrets
            try
            {
                appSecrets.S3 = await secretsManager.GetSecretAsync<AwsS3Secrets>(
                    "TransactionProcessor/AWS/S3", correlationId);
            }
            catch
            {
                appSecrets.S3 = new AwsS3Secrets
                {
                    BucketName = configuration["AWS:S3:BucketName"] ?? "cnab-files",
                    AccessKeyId = configuration["AWS:S3:AccessKeyId"] ?? "test",
                    SecretAccessKey = configuration["AWS:S3:SecretAccessKey"] ?? "test",
                    Region = configuration["AWS:Region"] ?? "us-east-1"
                };
            }

            // Load SQS secrets
            try
            {
                appSecrets.SQS = await secretsManager.GetSecretAsync<AwsSqsSecrets>(
                    "TransactionProcessor/AWS/SQS", correlationId);
            }
            catch
            {
                appSecrets.SQS = new AwsSqsSecrets
                {
                    QueueUrl = configuration["AWS:SQS:QueueUrl"] 
                        ?? "http://localstack:4566/000000000000/file-processing-queue",
                    DlqUrl = configuration["AWS:SQS:DlqUrl"] 
                        ?? configuration["AWS:SQS:DLQUrl"]
                        ?? "http://localstack:4566/000000000000/file-processing-dlq",
                    Region = configuration["AWS:Region"] ?? "us-east-1"
                };
            }

            return appSecrets;
        }
    }

    /// <summary>
    /// Configuration options for file processing worker.
    /// Loaded from appsettings.FileProcessing section.
    /// </summary>
    public class FileProcessingOptions
    {
        /// <summary>
        /// Delay in milliseconds when queue is empty (backoff).
        /// </summary>
        public int EmptyQueueDelayMs { get; set; } = 5000;

        /// <summary>
        /// Maximum messages to process in each batch.
        /// </summary>
        public int MaxMessagesPerBatch { get; set; } = 10;

        /// <summary>
        /// Visibility timeout in seconds for SQS messages.
        /// Must match SQS queue configuration.
        /// </summary>
        public int VisibilityTimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Enable structured logging with correlation IDs.
        /// </summary>
        public bool EnableStructuredLogging { get; set; } = true;
    }
}
