using FastEndpoints;
using FastEndpoints.Swagger;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSwag;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionProcessor.Api.Exceptions;
using TransactionProcessor.Api.Middleware;
using TransactionProcessor.Application.Queries.Files;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Infrastructure.Metrics;
using TransactionProcessor.Infrastructure.Persistence;
using TransactionProcessor.Infrastructure.Repositories;
using TransactionProcessor.Infrastructure.Secrets;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// LOGGING CONFIGURATION
// ============================================================================
// Configure Serilog for structured logging with JSON format
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()  // Includes CorrelationId from LogContext
        .Enrich.WithProperty("ApplicationName", "TransactionProcessor.Api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
        .WriteTo.Console()  // Write to console with structured format
        .WriteTo.File(
            path: "logs/transactionprocessor-.txt",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
});

Log.Information("TransactionProcessor API starting up...");

// ============================================================================
// SECRETS MANAGER CONFIGURATION
// ============================================================================
// Register Secrets Manager service for AWS Secrets Manager integration
// Supports both LocalStack (development) and AWS Secrets Manager (production)
builder.Services.AddSingleton<SecretsManagerService>();

Log.Information("Secrets Manager configured");

// ============================================================================
// DATABASE CONFIGURATION
// ============================================================================
// Get connection string from appsettings or environment variables
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found in configuration");

Log.Information("Configuring database with connection string: {ConnectionString}", 
    $"Host={builder.Configuration.GetConnectionString("Default")?.Split("Host=")[1]?.Split(";")[0]}");

// Configure Entity Framework Core with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
        .LogTo(Console.WriteLine)  // Log SQL queries in development
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

// ============================================================================
// DEPENDENCY INJECTION
// ============================================================================
// Register repositories
builder.Services.AddScoped<IFileRepository, FileRepository>();

// Register MediatR for query/command handling
builder.Services.AddMediatR(config =>
    config.RegisterServicesFromAssemblyContaining<GetFilesQuery>());

// ============================================================================
// METRICS CONFIGURATION (PROMETHEUS)
// ============================================================================
// Initialize Prometheus metrics (MetricsService uses static metrics)
// This ensures all metrics are registered with the default registry
builder.Services.AddSingleton(_ => new MetricsService());

Log.Information("Prometheus metrics initialized");

// ============================================================================
// FASTENDPOINTS CONFIGURATION
// ============================================================================
// Register FastEndpoints services
builder.Services.AddFastEndpoints();

// Register global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ============================================================================
// JSON SERIALIZATION CONFIGURATION
// ============================================================================
// Configure JSON serialization (camelCase, UTC datetime, enum as string)
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    
    // ISO 8601 UTC format for DateTime serialization
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opts.SerializerOptions.Converters.Add(new Rfc3339DateTimeConverter());
    opts.SerializerOptions.Converters.Add(new NullableDateTimeConverter());
});

// ============================================================================
// VALIDATION CONFIGURATION
// ============================================================================
// Register FluentValidation validators from assembly
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// ============================================================================
// SWAGGER/OPENAPI DOCUMENTATION
// ============================================================================
// Configure NSwag for OpenAPI documentation
builder.Services.AddOpenApiDocument(settings =>
{
    settings.Title = "TransactionProcessor API";
    settings.Version = "v1";
    settings.Description = "CNAB file processing and transaction management API";
    
    // Add OAuth2 authentication documentation
    settings.AddAuth("Bearer", new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "Bearer",
        Description = "JWT Bearer token authentication",
        Name = "Authorization",
        In = OpenApiSecurityApiKeyLocation.Header
    });
});

var app = builder.Build();

// ============================================================================
// MIDDLEWARE PIPELINE CONFIGURATION
// ============================================================================

// Add global exception handling middleware
app.UseExceptionHandler();

// Add correlation ID middleware (must be early in pipeline)
app.UseCorrelationId();

// Add security headers middleware
app.UseSecurityHeaders();

// Add metrics middleware (track HTTP metrics)
app.UseMetrics();

// ============================================================================
// LOGGING MIDDLEWARE
// ============================================================================
// Use Serilog for structured request/response logging
// Note: This logs request details for debugging
app.UseSerilogRequestLogging();

// ============================================================================
// HTTPS ENFORCEMENT
// ============================================================================
// Redirect HTTP to HTTPS (except in development for testing)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    Log.Information("HTTPS redirection enabled (production)");
}
else
{
    Log.Information("HTTPS redirection disabled (development)");
}

// ============================================================================
// SWAGGER/OPENAPI DOCUMENTATION
// ============================================================================
// Enable Swagger UI and OpenAPI specification in all environments
app.UseOpenApi();
app.UseSwaggerUi(settings =>
{
    settings.Path = "/swagger";
    settings.DocumentTitle = "TransactionProcessor API Documentation";
});

Log.Information("Swagger UI available at /swagger");
Log.Information("OpenAPI specification available at /swagger/v1.json");

// ============================================================================
// FASTENDPOINTS MIDDLEWARE
// ============================================================================
// Register FastEndpoints middleware (must be after exception handler)
app.UseFastEndpoints(config =>
{
    // Configure FastEndpoints routing with api prefix
    config.Endpoints.RoutePrefix = "api";
});

// ============================================================================
// APPLICATION STARTUP
// ============================================================================
Log.Information("TransactionProcessor API initialized successfully");
Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
Log.Information("API will be available at: https://localhost:5001");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

namespace TransactionProcessor.Api
{
    /// <summary>
    /// Program class for test framework integration
    /// </summary>
    public partial class Program { }
}
