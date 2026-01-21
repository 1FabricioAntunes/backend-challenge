using FastEndpoints;
using FastEndpoints.Swagger;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionProcessor.Api.Exceptions;
using TransactionProcessor.Api.Extensions;
using TransactionProcessor.Api.Middleware;
using TransactionProcessor.Api.Models;
using Amazon;
using Amazon.S3;
using Amazon.SQS;
using TransactionProcessor.Application.Queries.Files;
using TransactionProcessor.Application.Services;
using TransactionProcessor.Domain.Interfaces;
using TransactionProcessor.Domain.Repositories;
using TransactionProcessor.Domain.Services;
using TransactionProcessor.Infrastructure.Authentication;
using TransactionProcessor.Infrastructure.Messaging;
using TransactionProcessor.Infrastructure.Metrics;
using TransactionProcessor.Infrastructure.Persistence;
using TransactionProcessor.Infrastructure.Repositories;
using TransactionProcessor.Infrastructure.Secrets;
using TransactionProcessor.Infrastructure.Storage;

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
// Generate correlation ID for startup tracking
var startupCorrelationId = Guid.NewGuid().ToString();

// Load secrets from AWS Secrets Manager (LocalStack in dev, AWS in prod)
// Secrets are loaded at startup, validated, and cached in memory (fail-fast pattern)
// This returns the loaded secrets for immediate use
var appSecrets = builder.Services.AddSecretsManagementAndGetSecrets(builder.Configuration, startupCorrelationId);

// ============================================================================
// DATABASE CONFIGURATION
// ============================================================================
// Get connection string from loaded secrets (already validated)
var connectionString = appSecrets.Database.ConnectionString;

Log.Information("[{CorrelationId}] Configuring database connection", startupCorrelationId);

// Configure Entity Framework Core with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
        .LogTo(Console.WriteLine)  // Log SQL queries in development
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

// ============================================================================
// DEPENDENCY INJECTION
// ============================================================================
// Register AWS S3 client (supports LocalStack for development)
var s3ServiceUrl = builder.Configuration["AWS:S3:ServiceUrl"];
var sqsServiceUrl = builder.Configuration["AWS:SQS:ServiceUrl"];
var awsRegion = builder.Configuration["AWS:Region"] ?? "us-east-1";

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var config = new AmazonS3Config
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion)
    };
    
    // Configure for LocalStack if service URL is provided
    if (!string.IsNullOrEmpty(s3ServiceUrl))
    {
        config.ServiceURL = s3ServiceUrl;
        config.ForcePathStyle = true; // Required for LocalStack
    }
    
    return new AmazonS3Client(config);
});

// Register AWS SQS client (supports LocalStack for development)
builder.Services.AddSingleton<IAmazonSQS>(sp =>
{
    var config = new AmazonSQSConfig
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(awsRegion)
    };
    
    // Configure for LocalStack if service URL is provided
    if (!string.IsNullOrEmpty(sqsServiceUrl))
    {
        config.ServiceURL = sqsServiceUrl;
    }
    
    return new AmazonSQSClient(config);
});

// Register repositories
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IStoreRepository, StoreRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Register domain services
builder.Services.AddScoped<IFileValidator, FileValidator>();

// Register application services
builder.Services.AddScoped<ICNABParser, CNABParser>();
builder.Services.AddScoped<ICNABValidator, CNABValidator>();

// Register infrastructure services
builder.Services.AddScoped<IFileStorageService, S3FileStorageService>();
builder.Services.AddScoped<IMessageQueueService, SQSMessageQueueService>();

// Register mock Cognito service for development (when LocalStack Cognito unavailable)
var isDevelopment = builder.Environment.IsDevelopment();
var serviceUrl = builder.Configuration["AWS:SecretsManager:ServiceUrl"];
if (isDevelopment && !string.IsNullOrEmpty(serviceUrl))
{
    builder.Services.AddSingleton<MockCognitoService>();
    Log.Information("Mock Cognito service registered for development (fallback when LocalStack Cognito unavailable)");
}

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
// Register FastEndpoints services and Swagger documentation
builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.Title = "TransactionProcessor API";
            s.Version = "v1";
            s.Description = "CNAB file processing and transaction management API";
        };
        
        // Configure JWT Bearer authentication for Swagger
        o.EnableJWTBearerAuth = true;
    });

// Register global exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ============================================================================
// CORS CONFIGURATION
// ============================================================================
// Configure CORS from appsettings
var corsConfig = builder.Configuration.GetSection("CORS");
var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var allowHeaders = corsConfig.GetSection("AllowHeaders").Get<string[]>() ?? new[] { "*" };
var allowMethods = corsConfig.GetSection("AllowMethods").Get<string[]>() ?? new[] { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
var allowCredentials = corsConfig.GetValue<bool>("AllowCredentials", false);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            // If no origins specified, allow all (development only)
            policy.AllowAnyOrigin();
        }
        
        policy.WithHeaders(allowHeaders);
        policy.WithMethods(allowMethods);
        
        if (allowCredentials && allowedOrigins.Length > 0)
        {
            policy.AllowCredentials();
        }
    });
});

Log.Information("CORS configured with {OriginCount} allowed origin(s)", allowedOrigins.Length);

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
// AUTHENTICATION & AUTHORIZATION CONFIGURATION
// ============================================================================
// Configure OAuth2/JWT authentication with AWS Cognito
Log.Information("[{CorrelationId}] Configuring authentication with Cognito", startupCorrelationId);

// Load authentication options from configuration
var authOptions = builder.Configuration.GetSection("AWS:Cognito").Get<AuthenticationOptions>()
    ?? throw new InvalidOperationException("Cognito configuration is missing from appsettings");

// Validate authentication configuration (fail-fast pattern)
authOptions.Validate();

// Register authentication options for dependency injection
builder.Services.AddSingleton(authOptions);

// Configure JWT Bearer authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // OAuth2 authority (token issuer)
    // LocalStack: http://localhost:4566
    // AWS Cognito: https://cognito-idp.{region}.amazonaws.com/{userPoolId}
    // For mock auth in development, set to mock-issuer to avoid metadata fetch
    var isDevelopment = builder.Environment.IsDevelopment();
    var serviceUrl = builder.Configuration["AWS:SecretsManager:ServiceUrl"];
    var useMockAuth = isDevelopment && !string.IsNullOrEmpty(serviceUrl);
    
    if (useMockAuth)
    {
        // Skip metadata endpoint for mock authentication
        options.Authority = "mock-issuer";
        // Don't set MetadataAddress - let it use default behavior
        // MetadataAddress is not needed for mock authentication
    }
    else
    {
        options.Authority = authOptions.Authority;
    }
    
    // Audience validation ('aud' claim in JWT)
    // OWASP A01: Broken Access Control prevention
    options.Audience = authOptions.Audience;
    
    // HTTPS metadata requirement
    // Production: true (HTTPS enforcement for OIDC metadata)
    // LocalStack: false (HTTP only in local development)
    // OWASP A02: Cryptographic Failures mitigation
    options.RequireHttpsMetadata = useMockAuth ? false : authOptions.RequireHttpsMetadata;
    
    // Save token in AuthenticationProperties for later use
    options.SaveToken = true;
    
    // Token validation parameters
    // Reference: technical-decisions.md JWT Security Considerations
    
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // ✅ Validate token signature using RS256 (skip for mock tokens in development)
        // CVE-2015-9235: Algorithm confusion attack prevention
        ValidateIssuerSigningKey = useMockAuth ? false : authOptions.ValidateIssuerSigningKey,
        
        // ✅ Validate issuer ('iss' claim)
        // JWT Security: Prevent token substitution attacks
        // In development with mock auth, accept both real issuer and mock-issuer
        ValidateIssuer = authOptions.ValidateIssuer,
        ValidIssuers = useMockAuth 
            ? new[] { authOptions.Authority, "mock-issuer" }
            : new[] { authOptions.Authority },
        
        // ✅ Validate audience ('aud' claim)
        // OWASP A01: Prevent token reuse across APIs
        ValidateAudience = authOptions.ValidateAudience,
        ValidAudience = authOptions.Audience,
        
        // ✅ Validate token lifetime (exp, nbf claims)
        // OWASP A07: Identification and Authentication Failures prevention
        ValidateLifetime = authOptions.ValidateLifetime,
        
        // ✅ Clock skew tolerance (5 minutes default)
        // JWT Security: Reasonable tolerance for time differences
        ClockSkew = authOptions.ClockSkew,
        
        // ✅ Require expiration ('exp' claim must exist)
        // JWT Security: All tokens must have expiration
        RequireExpirationTime = true,
        
        // ✅ Require signed tokens (skip for mock tokens in development)
        // CVE-2015-9235: Algorithm confusion attack prevention
        RequireSignedTokens = !useMockAuth,
        
        // ✅ Valid algorithms: RS256 only (or HS256 for mock tokens in development)
        // JWT Security: Prevent weak algorithm attacks
        ValidAlgorithms = useMockAuth
            ? new[] { SecurityAlgorithms.RsaSha256, SecurityAlgorithms.HmacSha256 }
            : new[] { SecurityAlgorithms.RsaSha256 },
        
        // For mock tokens in development, use a simple symmetric key
        IssuerSigningKey = useMockAuth
            ? new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes("mock-signing-key-for-development-only-not-secure"))
            : null
    };
    
    // Event handlers for authentication lifecycle
    options.Events = new JwtBearerEvents
    {
        // Log authentication failures for security monitoring
        OnAuthenticationFailed = context =>
        {
            Log.Warning(
                "JWT authentication failed: {Exception}. Token: {Token}",
                context.Exception.Message,
                context.Request.Headers.Authorization.ToString().Substring(0, Math.Min(20, context.Request.Headers.Authorization.ToString().Length)) + "...");
            return Task.CompletedTask;
        },
        
        // Log successful token validation
        OnTokenValidated = context =>
        {
            var userId = context.Principal?.FindFirst("sub")?.Value ?? "unknown";
            Log.Information("JWT token validated successfully for user: {UserId}", userId);
            return Task.CompletedTask;
        },
        
        // Handle challenge (401 Unauthorized)
        OnChallenge = context =>
        {
            Log.Warning(
                "JWT authentication challenge: {Error}. Path: {Path}",
                context.Error ?? "No token provided",
                context.Request.Path);
            return Task.CompletedTask;
        }
    };
});

// Add authorization services
builder.Services.AddAuthorization();

Log.Information("[{CorrelationId}] Authentication configured: Authority={Authority}, Audience={Audience}",
    startupCorrelationId, authOptions.Authority, authOptions.Audience);

// ============================================================================
// SWAGGER/OPENAPI DOCUMENTATION
// ============================================================================
// Configure FastEndpoints Swagger (automatically discovers all FastEndpoints)
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "TransactionProcessor API";
        s.Version = "v1";
        s.Description = "CNAB file processing and transaction management API";
    };
    
    // Configure JWT Bearer authentication for Swagger
    o.EnableJWTBearerAuth = true;
});

var app = builder.Build();

// ============================================================================
// MIDDLEWARE PIPELINE CONFIGURATION
// ============================================================================

// Add global exception handling middleware
app.UseExceptionHandler();

// Add correlation ID middleware (must be early in pipeline)
app.UseCorrelationId();

// Add CORS middleware (must be before authentication and authorization)
app.UseCors();

// Add security headers middleware
app.UseSecurityHeaders();

// Add metrics middleware (track HTTP metrics)
app.UseMetrics();

// ============================================================================
// AUTHENTICATION & AUTHORIZATION MIDDLEWARE
// ============================================================================
// Add authentication middleware (validates JWT tokens)
// Must be before UseAuthorization and before FastEndpoints
app.UseAuthentication();

// Add authorization middleware (enforces [Authorize] attributes)
// OWASP A01: Broken Access Control prevention
app.UseAuthorization();

// Optional: Add custom JWT validation middleware for enhanced logging
// NOTE: The built-in UseAuthentication() already handles JWT validation.
// Uncomment the line below only if you need additional custom validation logic
// or enhanced security logging beyond the built-in handler.
// app.UseJwtValidation();

Log.Information("Authentication and authorization middleware enabled");

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
// FASTENDPOINTS MIDDLEWARE
// ============================================================================
// Register FastEndpoints middleware (must be after exception handler)
// UseSwaggerGen must come AFTER UseFastEndpoints to discover endpoints
app.UseFastEndpoints()
.UseSwaggerGen(); // Enable Swagger UI after FastEndpoints

Log.Information("Swagger UI available at /swagger");
Log.Information("OpenAPI specification available at /swagger/v1.json");

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
