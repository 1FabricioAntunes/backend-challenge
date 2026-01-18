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
        // Database
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString,
                npgsqlOptions => npgsqlOptions.CommandTimeout(60)));

        // Repositories
        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();

        // Application Services
        services.AddScoped<ICNABParser, CNABParser>();
        services.AddScoped<IFileProcessingService, FileProcessingService>();

        // Infrastructure Services
        services.AddScoped<IFileStorageService, S3FileStorageService>();
        services.AddScoped<IMessageQueueService, SQSMessageQueueService>();

        // Hosted Services
        services.AddHostedService<FileProcessingHostedService>();
        services.AddHostedService<NotificationDlqWorker>();

        // Configuration
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
