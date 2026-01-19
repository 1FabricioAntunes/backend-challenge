using FastEndpoints;
using FastEndpoints.Swagger;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using TransactionProcessor.Api.Exceptions;
using TransactionProcessor.Api.Extensions;
using TransactionProcessor.Api.Middleware;
using TransactionProcessor.Api.Models;
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
    options.Authority = authOptions.Authority;
    
    // Audience validation ('aud' claim in JWT)
    // OWASP A01: Broken Access Control prevention
    options.Audience = authOptions.Audience;
    
    // HTTPS metadata requirement
    // Production: true (HTTPS enforcement for OIDC metadata)
    // LocalStack: false (HTTP only in local development)
    // OWASP A02: Cryptographic Failures mitigation
    options.RequireHttpsMetadata = authOptions.RequireHttpsMetadata;
    
    // Save token in AuthenticationProperties for later use
    options.SaveToken = true;
    
    // Token validation parameters
    // Reference: technical-decisions.md JWT Security Considerations
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // ✅ Validate token signature using RS256
        // CVE-2015-9235: Algorithm confusion attack prevention
        ValidateIssuerSigningKey = authOptions.ValidateIssuerSigningKey,
        
        // ✅ Validate issuer ('iss' claim)
        // JWT Security: Prevent token substitution attacks
        ValidateIssuer = authOptions.ValidateIssuer,
        ValidIssuer = authOptions.Authority,
        
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
        
        // ✅ Require signed tokens (reject alg: none)
        // CVE-2015-9235: Algorithm confusion attack prevention
        RequireSignedTokens = true,
        
        // ✅ Valid algorithms: RS256 only
        // JWT Security: Prevent weak algorithm attacks
        ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
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
