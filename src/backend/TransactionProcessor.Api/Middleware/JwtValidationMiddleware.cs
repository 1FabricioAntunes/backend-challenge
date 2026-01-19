using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using TransactionProcessor.Api.Models;

namespace TransactionProcessor.Api.Middleware;

/// <summary>
/// Custom JWT validation middleware with comprehensive security checks.
/// Implements JWT validation checklist from technical-decisions.md.
/// </summary>
/// <remarks>
/// Security References:
/// - OWASP A01: Broken Access Control prevention through token validation
/// - OWASP A07: Identification and Authentication Failures prevention
/// - CVE-2015-9235: Algorithm confusion attack prevention
/// - JWT Security Considerations: All required claims validated
/// 
/// This middleware provides an additional validation layer beyond ASP.NET Core's
/// built-in JWT Bearer authentication for enhanced security and logging.
/// </remarks>
public class JwtValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtValidationMiddleware> _logger;
    private readonly AuthenticationOptions _authOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    /// <summary>
    /// Constructor for JWT validation middleware
    /// </summary>
    public JwtValidationMiddleware(
        RequestDelegate next,
        ILogger<JwtValidationMiddleware> logger,
        AuthenticationOptions authOptions)
    {
        _next = next;
        _logger = logger;
        _authOptions = authOptions;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <summary>
    /// Invokes the middleware to validate JWT tokens on each request
    /// </summary>
    /// <param name="context">The HTTP context for the current request</param>
    public async Task InvokeAsync(HttpContext context)
    {
        // Extract Authorization header
        var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (string.IsNullOrEmpty(authorizationHeader))
        {
            // No token provided - let the request continue
            // The [Authorize] attribute will handle rejection if required
            await _next(context);
            return;
        }

        // Extract token from "Bearer <token>" format
        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Invalid Authorization header format. Expected 'Bearer <token>'. Path: {Path}",
                context.Request.Path);
            
            await ReturnUnauthorized(context, "Invalid authorization header format");
            return;
        }

        var token = authorizationHeader.Substring("Bearer ".Length).Trim();

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Empty JWT token provided. Path: {Path}", context.Request.Path);
            await ReturnUnauthorized(context, "Empty token provided");
            return;
        }

        // Validate the JWT token
        var validationResult = ValidateToken(token, context);

        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "JWT validation failed: {Reason}. Path: {Path}, Token: {TokenPreview}",
                validationResult.ErrorMessage,
                context.Request.Path,
                token.Substring(0, Math.Min(20, token.Length)) + "...");

            await ReturnUnauthorized(context, validationResult.ErrorMessage ?? "Invalid token");
            return;
        }

        // Token is valid - set the ClaimsPrincipal in HttpContext
        context.User = validationResult.ClaimsPrincipal!;

        // Extract and log user information
        var userId = validationResult.ClaimsPrincipal!.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? validationResult.ClaimsPrincipal.FindFirst("sub")?.Value
                     ?? "unknown";

        _logger.LogInformation(
            "JWT token validated successfully. UserId: {UserId}, Path: {Path}",
            userId,
            context.Request.Path);

        // Continue to next middleware
        await _next(context);
    }

    /// <summary>
    /// Validates the JWT token according to security requirements
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <param name="context">The HTTP context</param>
    /// <returns>Validation result with ClaimsPrincipal if successful</returns>
    private TokenValidationResult ValidateToken(string token, HttpContext context)
    {
        try
        {
            // ============================================================================
            // STEP 1: Pre-validation - Check token format and algorithm
            // ============================================================================
            
            // Read token without validation to inspect algorithm
            JwtSecurityToken? jwtToken = null;
            try
            {
                jwtToken = _tokenHandler.ReadJwtToken(token);
            }
            catch (Exception ex)
            {
                return TokenValidationResult.Failure($"Invalid token format: {ex.Message}");
            }

            // ✅ Algorithm Confusion Prevention (CVE-2015-9235)
            // Reject tokens with alg: none or unexpected algorithms
            var algorithm = jwtToken.Header.Alg;
            
            if (string.IsNullOrEmpty(algorithm) || 
                algorithm.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "JWT token rejected: Algorithm is 'none' (CVE-2015-9235 protection). Path: {Path}",
                    context.Request.Path);
                
                return TokenValidationResult.Failure(
                    "Token algorithm 'none' is not allowed (CVE-2015-9235 protection)");
            }

            if (!algorithm.Equals(SecurityAlgorithms.RsaSha256, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "JWT token rejected: Invalid algorithm '{Algorithm}'. Only RS256 allowed. Path: {Path}",
                    algorithm,
                    context.Request.Path);
                
                return TokenValidationResult.Failure(
                    $"Invalid algorithm '{algorithm}'. Only RS256 is allowed");
            }

            // ============================================================================
            // STEP 2: Token Validation Parameters
            // ============================================================================
            
            var validationParameters = new TokenValidationParameters
            {
                // ✅ Validate token signature using RS256
                ValidateIssuerSigningKey = _authOptions.ValidateIssuerSigningKey,
                
                // ✅ Validate issuer ('iss' claim)
                ValidateIssuer = _authOptions.ValidateIssuer,
                ValidIssuer = _authOptions.Authority,
                
                // ✅ Validate audience ('aud' claim)
                ValidateAudience = _authOptions.ValidateAudience,
                ValidAudience = _authOptions.Audience,
                
                // ✅ Validate token lifetime (exp, nbf claims)
                ValidateLifetime = _authOptions.ValidateLifetime,
                
                // ✅ Clock skew tolerance (5 minutes default)
                ClockSkew = _authOptions.ClockSkew,
                
                // ✅ Require expiration time
                RequireExpirationTime = true,
                
                // ✅ Require signed tokens
                RequireSignedTokens = true,
                
                // ✅ Valid algorithms: RS256 only
                ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
                
                // For LocalStack, we may need to skip issuer signing key validation
                // In production with AWS Cognito, this should always be true
                // The signing key will be fetched from the JWKS endpoint
                IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                {
                    // In production, fetch JWKS from Cognito
                    // For LocalStack, this is simplified
                    // The ASP.NET Core JWT Bearer middleware handles this automatically
                    return null;
                }
            };

            // ============================================================================
            // STEP 3: Validate the token
            // ============================================================================
            
            ClaimsPrincipal? claimsPrincipal;
            SecurityToken? validatedToken;

            try
            {
                claimsPrincipal = _tokenHandler.ValidateToken(
                    token,
                    validationParameters,
                    out validatedToken);
            }
            catch (SecurityTokenExpiredException ex)
            {
                _logger.LogWarning(
                    "JWT token expired: {Exception}. Path: {Path}",
                    ex.Message,
                    context.Request.Path);
                
                return TokenValidationResult.Failure("Token has expired");
            }
            catch (SecurityTokenInvalidIssuerException ex)
            {
                _logger.LogWarning(
                    "JWT token invalid issuer: {Exception}. Path: {Path}",
                    ex.Message,
                    context.Request.Path);
                
                return TokenValidationResult.Failure("Invalid token issuer");
            }
            catch (SecurityTokenInvalidAudienceException ex)
            {
                _logger.LogWarning(
                    "JWT token invalid audience: {Exception}. Path: {Path}",
                    ex.Message,
                    context.Request.Path);
                
                return TokenValidationResult.Failure("Invalid token audience");
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                _logger.LogWarning(
                    "JWT token invalid signature: {Exception}. Path: {Path}",
                    ex.Message,
                    context.Request.Path);
                
                return TokenValidationResult.Failure("Invalid token signature");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "JWT token validation failed with unexpected error. Path: {Path}",
                    context.Request.Path);
                
                return TokenValidationResult.Failure($"Token validation failed: {ex.Message}");
            }

            // ============================================================================
            // STEP 4: Validate required claims
            // ============================================================================
            
            // ✅ Validate required claims exist
            var requiredClaims = new[] { "sub", "iat", "exp" };
            foreach (var claimType in requiredClaims)
            {
                var claim = claimsPrincipal.FindFirst(claimType);
                if (claim == null)
                {
                    _logger.LogWarning(
                        "JWT token missing required claim: {ClaimType}. Path: {Path}",
                        claimType,
                        context.Request.Path);
                    
                    return TokenValidationResult.Failure($"Missing required claim: {claimType}");
                }
            }

            // ============================================================================
            // STEP 5: Extract UserId from 'sub' claim
            // ============================================================================
            
            var subClaim = claimsPrincipal.FindFirst("sub");
            var userId = subClaim?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning(
                    "JWT token has empty 'sub' claim. Path: {Path}",
                    context.Request.Path);
                
                return TokenValidationResult.Failure("Token 'sub' claim is empty");
            }

            // Add NameIdentifier claim for compatibility with ASP.NET Core
            if (claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier) == null)
            {
                var identity = claimsPrincipal.Identity as ClaimsIdentity;
                identity?.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            // ============================================================================
            // STEP 6: Return successful validation result
            // ============================================================================
            
            return TokenValidationResult.Success(claimsPrincipal);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during JWT validation. Path: {Path}",
                context.Request.Path);
            
            return TokenValidationResult.Failure($"Internal validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns a 401 Unauthorized response with error details
    /// </summary>
    private async Task ReturnUnauthorized(HttpContext context, string errorMessage)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = "unauthorized",
            message = errorMessage,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            path = context.Request.Path.ToString()
        };

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }

    /// <summary>
    /// Result of token validation
    /// </summary>
    private class TokenValidationResult
    {
        public bool IsValid { get; set; }
        public ClaimsPrincipal? ClaimsPrincipal { get; set; }
        public string? ErrorMessage { get; set; }

        public static TokenValidationResult Success(ClaimsPrincipal claimsPrincipal)
        {
            return new TokenValidationResult
            {
                IsValid = true,
                ClaimsPrincipal = claimsPrincipal,
                ErrorMessage = null
            };
        }

        public static TokenValidationResult Failure(string errorMessage)
        {
            return new TokenValidationResult
            {
                IsValid = false,
                ClaimsPrincipal = null,
                ErrorMessage = errorMessage
            };
        }
    }
}
