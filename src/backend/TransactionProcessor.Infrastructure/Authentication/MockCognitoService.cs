using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TransactionProcessor.Infrastructure.Authentication;

/// <summary>
/// Mock Cognito service that implements Cognito-compatible authentication
/// for local development when LocalStack Cognito is not available.
/// 
/// This service:
/// - Implements InitiateAuth API compatible with AWS Cognito
/// - Returns properly signed JWT tokens
/// - Supports USER_PASSWORD_AUTH flow
/// - Validates credentials against a simple user store
/// 
/// Note: This is for development only. Production uses AWS Cognito.
/// </summary>
public class MockCognitoService
{
    private readonly ILogger<MockCognitoService> _logger;
    private readonly Dictionary<string, MockUser> _users;
    private readonly string _signingKey;

    public MockCognitoService(ILogger<MockCognitoService> logger)
    {
        _logger = logger;
        _signingKey = "mock-signing-key-for-development-only-not-secure";
        
        // Initialize with test user
        _users = new Dictionary<string, MockUser>
        {
            {
                "test@transactionprocessor.local",
                new MockUser
                {
                    Email = "test@transactionprocessor.local",
                    Name = "Test User",
                    Password = "TestPassword123!", // In real Cognito, this would be hashed
                    EmailVerified = true
                }
            }
        };
    }

    /// <summary>
    /// Authenticate user with email and password (USER_PASSWORD_AUTH flow)
    /// </summary>
    public async Task<MockAuthResult> InitiateAuthAsync(string clientId, string email, string password, CancellationToken ct = default)
    {
        await Task.Delay(100, ct); // Simulate network delay

        _logger.LogInformation("Mock Cognito: Authenticating user {Email}", email);

        // Validate user exists
        if (!_users.TryGetValue(email, out var user))
        {
            _logger.LogWarning("Mock Cognito: User not found: {Email}", email);
            throw new MockCognitoException("User does not exist.", "UserNotFoundException");
        }

        // Validate password
        if (user.Password != password)
        {
            _logger.LogWarning("Mock Cognito: Invalid password for user {Email}", email);
            throw new MockCognitoException("Incorrect username or password.", "NotAuthorizedException");
        }

        // Generate JWT token
        var accessToken = GenerateJwtToken(user, clientId, "access");
        var idToken = GenerateJwtToken(user, clientId, "id");
        var refreshToken = GenerateRefreshToken();

        _logger.LogInformation("Mock Cognito: Authentication successful for {Email}", email);

        return new MockAuthResult
        {
            AccessToken = accessToken,
            IdToken = idToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = 3600 // 1 hour
        };
    }

    /// <summary>
    /// Get user details by email
    /// </summary>
    public async Task<MockUser> GetUserAsync(string email, CancellationToken ct = default)
    {
        await Task.Delay(50, ct); // Simulate network delay

        if (!_users.TryGetValue(email, out var user))
        {
            throw new MockCognitoException("User does not exist.", "UserNotFoundException");
        }

        return user;
    }

    /// <summary>
    /// Generate a JWT token compatible with Cognito format
    /// </summary>
    private string GenerateJwtToken(MockUser user, string clientId, string tokenType)
    {
        var header = new
        {
            alg = "HS256",
            typ = "JWT"
        };

        var now = DateTimeOffset.UtcNow;
        var payload = new
        {
            sub = user.Email,
            email = user.Email,
            email_verified = user.EmailVerified,
            name = user.Name,
            iss = "mock-issuer",
            aud = "transactionprocessor-local",
            token_use = tokenType,
            auth_time = now.ToUnixTimeSeconds(),
            iat = now.ToUnixTimeSeconds(),
            exp = now.AddHours(24).ToUnixTimeSeconds(),
            client_id = clientId
        };

        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        // Sign with HMAC-SHA256
        var dataToSign = Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingKey));
        var signatureBytes = hmac.ComputeHash(dataToSign);
        var signature = Base64UrlEncode(signatureBytes);

        return $"{headerBase64}.{payloadBase64}.{signature}";
    }

    private string GenerateRefreshToken()
    {
        // Simple refresh token (in real Cognito, this would be more complex)
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"refresh-{Guid.NewGuid()}"));
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
/// Mock user model
/// </summary>
public class MockUser
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
}

/// <summary>
/// Mock authentication result
/// </summary>
public class MockAuthResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string IdToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Mock Cognito exception
/// </summary>
public class MockCognitoException : Exception
{
    public string ErrorCode { get; }

    public MockCognitoException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }
}
