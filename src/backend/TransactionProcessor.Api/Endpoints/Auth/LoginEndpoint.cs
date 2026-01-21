using FastEndpoints;
using Microsoft.Extensions.Configuration;
using Serilog.Context;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TransactionProcessor.Infrastructure.Authentication;
using ApiErrorResponse = TransactionProcessor.Api.Models.ErrorResponse;

namespace TransactionProcessor.Api.Endpoints.Auth;

/// <summary>
/// Login endpoint for authenticating users with LocalStack Cognito.
/// 
/// Route: POST /api/auth/v1/login
/// 
/// Request:
/// - Email: User email address
/// - Password: User password
/// 
/// Response (200 OK):
/// - User: User information (name, email)
/// - Token: JWT access token
/// 
/// Error Responses:
/// - 400 Bad Request: Invalid request (missing email/password)
/// - 401 Unauthorized: Invalid credentials
/// - 500 Internal Server Error: Server error
/// 
/// Reference: docs/security.md § Authentication
/// </summary>
public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginEndpoint> _logger;
    private readonly MockCognitoService? _mockCognitoService;

    public LoginEndpoint(
        IConfiguration configuration, 
        ILogger<LoginEndpoint> logger,
        MockCognitoService? mockCognitoService = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mockCognitoService = mockCognitoService;
    }

    public override void Configure()
    {
        Post("/api/auth/v1/login");
        AllowAnonymous(); // Login endpoint must be accessible without authentication
        Description(d => d
            .WithTags("Authentication")
            .Produces<LoginResponse>(200, "application/json")
            .Produces(400)
            .Produces(401)
            .Produces(500));
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        using (LogContext.PushProperty("CorrelationId", req.CorrelationId))
        {
            _logger.LogInformation("[{CorrelationId}] Login attempt for email: {Email}", req.CorrelationId, req.Email);

            try
            {
                // Always try Cognito first - preserve the Cognito call
                // Get Cognito configuration
                var cognitoConfig = _configuration.GetSection("AWS:Cognito");
                var authority = cognitoConfig["Authority"] ?? throw new InvalidOperationException("Cognito Authority not configured");
                var clientId = cognitoConfig["ClientId"] ?? throw new InvalidOperationException("Cognito ClientId not configured");
                var region = _configuration["AWS:Region"] ?? "us-east-1";
                var serviceUrl = _configuration["AWS:SecretsManager:ServiceUrl"]; // LocalStack endpoint

                // Get user pool ID from authority or configuration
                var userPoolId = cognitoConfig["UserPoolId"];
                if (string.IsNullOrEmpty(userPoolId))
                {
                    // Try to get from authority or use default
                    userPoolId = await GetUserPoolIdAsync(serviceUrl, region, ct);
                }

                // If user pool ID is still not found, check if we can fall back to mock service
                if (string.IsNullOrEmpty(userPoolId))
                {
                    var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
                    if (environment == "Development" && _mockCognitoService != null)
                    {
                        _logger.LogWarning("[{CorrelationId}] User pool ID not found - LocalStack Cognito unavailable. Falling back to mock Cognito service.", req.CorrelationId);
                        // Fall through to mock service fallback (will be caught by InternalErrorException handler)
                        throw new Amazon.CognitoIdentityProvider.Model.InternalErrorException("Cognito user pool not configured - using mock service");
                    }
                    else
                    {
                        _logger.LogError("[{CorrelationId}] User pool ID not found and mock service not available", req.CorrelationId);
                        HttpContext.Response.StatusCode = 500;
                        await HttpContext.Response.WriteAsJsonAsync(
                            new ApiErrorResponse(
                                "Cognito user pool not configured.",
                                "COGNITO_CONFIG_ERROR",
                                500),
                            cancellationToken: ct);
                        return;
                    }
                }
                
                _logger.LogDebug("[{CorrelationId}] Attempting authentication with AWS Cognito (User Pool: {UserPoolId})", req.CorrelationId, userPoolId);

                // Create Cognito client with timeout to prevent hanging
                IAmazonCognitoIdentityProvider cognitoClient;
                if (!string.IsNullOrEmpty(serviceUrl))
                {
                    // LocalStack configuration
                    var credentials = new BasicAWSCredentials("test", "test");
                    cognitoClient = new AmazonCognitoIdentityProviderClient(
                        credentials,
                        new AmazonCognitoIdentityProviderConfig
                        {
                            ServiceURL = serviceUrl,
                            AuthenticationRegion = region,
                            Timeout = TimeSpan.FromSeconds(10) // Timeout to fail fast if service unavailable
                        });
                }
                else
                {
                    // AWS Cognito configuration (production)
                    cognitoClient = new AmazonCognitoIdentityProviderClient(
                        new AmazonCognitoIdentityProviderConfig
                        {
                            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
                            Timeout = TimeSpan.FromSeconds(10)
                        });
                }

                // Authenticate user with Cognito
                // Use a cancellation token with timeout to prevent hanging
                using var authTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                authTimeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                
                var authRequest = new InitiateAuthRequest
                {
                    ClientId = clientId,
                    AuthFlow = AuthFlowType.USER_PASSWORD_AUTH,
                    AuthParameters = new Dictionary<string, string>
                    {
                        { "USERNAME", req.Email },
                        { "PASSWORD", req.Password }
                    }
                };

                var authResponse = await cognitoClient.InitiateAuthAsync(authRequest, authTimeoutCts.Token);

                if (authResponse.AuthenticationResult == null)
                {
                    _logger.LogWarning("[{CorrelationId}] Authentication failed: No authentication result", req.CorrelationId);
                    HttpContext.Response.StatusCode = 401;
                    await HttpContext.Response.WriteAsJsonAsync(
                        new ApiErrorResponse(
                            "Invalid credentials.",
                            "AUTHENTICATION_FAILED",
                            401),
                        cancellationToken: ct);
                    return;
                }

                // Get user details
                string name;
                try
                {
                    var getUserRequest = new AdminGetUserRequest
                    {
                        UserPoolId = userPoolId,
                        Username = req.Email
                    };

                    var userResponse = await cognitoClient.AdminGetUserAsync(getUserRequest, ct);

                    // Extract user name from attributes
                    var nameAttribute = userResponse.UserAttributes.FirstOrDefault(a => a.Name == "name");
                    name = nameAttribute?.Value ?? userResponse.Username;
                }
                catch (UserNotFoundException)
                {
                    // User not found - use email as name
                    _logger.LogWarning("[{CorrelationId}] User not found in user pool, using email as name", req.CorrelationId);
                    name = req.Email;
                }

                _logger.LogInformation("[{CorrelationId}] Login successful for user: {Email} (Authenticated via: AWS Cognito)", req.CorrelationId, req.Email);

                // Return response with token and user info
                HttpContext.Response.StatusCode = 200;
                var response = new LoginResponse
                {
                    User = new UserInfo
                    {
                        Name = name,
                        Email = req.Email
                    },
                    Token = authResponse.AuthenticationResult.AccessToken
                };
                
                // Add debug info in development mode
                var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
                if (environment == "Development")
                {
                    response.AuthMethod = "AWS Cognito";
                }
                
                await HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: ct);
            }
            catch (NotAuthorizedException ex)
            {
                _logger.LogWarning("[{CorrelationId}] Authentication failed: {Message}", req.CorrelationId, ex.Message);
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        "Invalid email or password.",
                        "AUTHENTICATION_FAILED",
                        401),
                    cancellationToken: ct);
            }
            catch (UserNotFoundException ex)
            {
                _logger.LogWarning("[{CorrelationId}] User not found: {Message}", req.CorrelationId, ex.Message);
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        "Invalid email or password.",
                        "AUTHENTICATION_FAILED",
                        401),
                    cancellationToken: ct);
            }
            catch (InvalidParameterException ex) when (ex.Message.Contains("password") || ex.Message.Contains("username") || ex.Message.Contains("USERNAME") || ex.Message.Contains("PASSWORD"))
            {
                // Cognito may throw InvalidParameterException for wrong credentials
                _logger.LogWarning("[{CorrelationId}] Invalid credentials: {Message}", req.CorrelationId, ex.Message);
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        "Invalid email or password.",
                        "AUTHENTICATION_FAILED",
                        401),
                    cancellationToken: ct);
            }
            catch (AmazonCognitoIdentityProviderException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized || ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // Catch any other Cognito exceptions that indicate authentication failure
                _logger.LogWarning("[{CorrelationId}] Authentication failed: {ErrorCode} - {Message}", req.CorrelationId, ex.ErrorCode, ex.Message);
                HttpContext.Response.StatusCode = 401;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        "Invalid email or password.",
                        "AUTHENTICATION_FAILED",
                        401),
                    cancellationToken: ct);
            }
            catch (Amazon.CognitoIdentityProvider.Model.InternalErrorException ex) when (ex.Message.Contains("not included in your current license") || ex.Message.Contains("not yet been emulated") || ex.Message.Contains("using mock service"))
            {
                // LocalStack Cognito not available - log error and provide helpful message
                _logger.LogWarning("[{CorrelationId}] LocalStack Cognito service not available: {Message}. Using mock Cognito service instead.", req.CorrelationId, ex.Message);
                
                var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development";
                if (environment == "Development" && _mockCognitoService != null)
                {
                    _logger.LogInformation("[{CorrelationId}] Authenticating with Mock Cognito Service (LocalStack Cognito unavailable)", req.CorrelationId);
                    
                    try
                    {
                        // Use mock Cognito service that implements Cognito-compatible API
                        var cognitoConfig = _configuration.GetSection("AWS:Cognito");
                        var clientId = cognitoConfig["ClientId"] ?? "test-client";
                        
                        var authResult = await _mockCognitoService.InitiateAuthAsync(clientId, req.Email, req.Password, ct);
                        var user = await _mockCognitoService.GetUserAsync(req.Email, ct);
                        
                        _logger.LogInformation("[{CorrelationId}] Login successful for user: {Email} (Authenticated via: Mock Cognito Service - LocalStack Cognito unavailable)", req.CorrelationId, req.Email);
                        
                        HttpContext.Response.StatusCode = 200;
                        var mockResponse = new LoginResponse
                        {
                            User = new UserInfo
                            {
                                Name = user.Name,
                                Email = user.Email
                            },
                            Token = authResult.AccessToken,
                            AuthMethod = "Mock Cognito Service (LocalStack unavailable)"
                        };
                        await HttpContext.Response.WriteAsJsonAsync(mockResponse, cancellationToken: ct);
                        return;
                    }
                    catch (MockCognitoException mockEx)
                    {
                        _logger.LogWarning("[{CorrelationId}] Mock Cognito authentication failed: {ErrorCode} - {Message}", req.CorrelationId, mockEx.ErrorCode, mockEx.Message);
                        HttpContext.Response.StatusCode = 401;
                        await HttpContext.Response.WriteAsJsonAsync(
                            new ApiErrorResponse(
                                "Invalid email or password.",
                                "AUTHENTICATION_FAILED",
                                401),
                            cancellationToken: ct);
                        return;
                    }
                    catch (Exception mockFallbackEx)
                    {
                        // Catch any unexpected exceptions in mock service fallback
                        _logger.LogError(mockFallbackEx, "[{CorrelationId}] Unexpected error in mock Cognito service fallback", req.CorrelationId);
                        HttpContext.Response.StatusCode = 401;
                        await HttpContext.Response.WriteAsJsonAsync(
                            new ApiErrorResponse(
                                "Invalid email or password.",
                                "AUTHENTICATION_FAILED",
                                401),
                            cancellationToken: ct);
                        return;
                    }
                }
                else
                {
                    // Production - don't fall back to mock
                    _logger.LogError(ex, "[{CorrelationId}] Cognito service unavailable in production", req.CorrelationId);
                    HttpContext.Response.StatusCode = 503;
                    await HttpContext.Response.WriteAsJsonAsync(
                        new ApiErrorResponse(
                            "Authentication service temporarily unavailable. Please ensure LocalStack Cognito is properly configured.",
                            "SERVICE_UNAVAILABLE",
                            503),
                        cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{CorrelationId}] Error during login", req.CorrelationId);
                HttpContext.Response.StatusCode = 500;
                await HttpContext.Response.WriteAsJsonAsync(
                    new ApiErrorResponse(
                        "An error occurred during authentication.",
                        "INTERNAL_ERROR",
                        500),
                    cancellationToken: ct);
            }
        }
    }

    private async Task<string?> GetUserPoolIdAsync(string? serviceUrl, string region, CancellationToken ct)
    {
        try
        {
            IAmazonCognitoIdentityProvider client;
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                var credentials = new BasicAWSCredentials("test", "test");
                client = new AmazonCognitoIdentityProviderClient(
                    credentials,
                    new AmazonCognitoIdentityProviderConfig
                    {
                        ServiceURL = serviceUrl,
                        AuthenticationRegion = region,
                        Timeout = TimeSpan.FromSeconds(5) // Short timeout to fail fast if Cognito unavailable
                    });
            }
            else
            {
                client = new AmazonCognitoIdentityProviderClient(
                    new AmazonCognitoIdentityProviderConfig
                    {
                        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
                        Timeout = TimeSpan.FromSeconds(5)
                    });
            }

            // Use a cancellation token with timeout to prevent hanging
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            
            var pools = await client.ListUserPoolsAsync(new ListUserPoolsRequest { MaxResults = 10 }, timeoutCts.Token);
            var pool = pools.UserPools.FirstOrDefault(p => p.Name == "transaction-processor-pool");
            return pool?.Id;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("User pool ID lookup timed out - Cognito service may be unavailable");
            return null;
        }
        catch (Amazon.CognitoIdentityProvider.Model.InternalErrorException ex) when (ex.Message.Contains("not included in your current license") || ex.Message.Contains("not yet been emulated"))
        {
            _logger.LogWarning("LocalStack Cognito not available: {Message}", ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve user pool ID, will try to use configured value");
            return null;
        }
    }

    /// <summary>
    /// Generate a mock JWT token for development (when Cognito is not available)
    /// This is NOT a real Cognito token, but allows development to proceed
    /// Uses HS256 with a simple signing key that matches JWT validation configuration
    /// </summary>
    private string GenerateMockJwtToken(string email, string name)
    {
        // Mock JWT token for development
        // Format: header.payload.signature (base64url encoded)
        // Uses HS256 with signing key that matches Program.cs configuration
        
        var header = new
        {
            alg = "HS256",
            typ = "JWT"
        };
        
        var payload = new
        {
            sub = email,
            email = email,
            name = name,
            iss = "mock-issuer",
            aud = "transactionprocessor-local",
            exp = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds(),
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);
        
        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        
        // Sign with HMAC-SHA256 using the same key as in Program.cs
        // Key: "mock-signing-key-for-development-only-not-secure"
        var signingKey = Encoding.UTF8.GetBytes("mock-signing-key-for-development-only-not-secure");
        var dataToSign = Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}");
        
        using var hmac = new HMACSHA256(signingKey);
        var signatureBytes = hmac.ComputeHash(dataToSign);
        var signature = Base64UrlEncode(signatureBytes);
        
        return $"{headerBase64}.{payloadBase64}.{signature}";
    }
    
    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}

/// <summary>
/// Login request model
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
}

/// <summary>
/// Login response model
/// </summary>
public class LoginResponse
{
    public UserInfo User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Authentication method used (only included in Development mode for debugging)
    /// Values: "AWS Cognito" or "Mock Cognito Service (LocalStack unavailable)"
    /// </summary>
    public string? AuthMethod { get; set; }
}

/// <summary>
/// User information
/// </summary>
public class UserInfo
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
