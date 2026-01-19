namespace TransactionProcessor.Api.Models;

/// <summary>
/// Configuration options for OAuth2/JWT authentication with AWS Cognito.
/// </summary>
/// <remarks>
/// Security References:
/// - OWASP A07: Identification and Authentication Failures prevention
/// - JWT validation per technical-decisions.md JWT Security Considerations
/// - Cognito integration per technical-decisions.md § 10 Security
/// </remarks>
public class AuthenticationOptions
{
    /// <summary>
    /// The OAuth2 authority URL (token issuer).
    /// For LocalStack: http://localhost:4566
    /// For AWS Cognito: https://cognito-idp.{region}.amazonaws.com/{userPoolId}
    /// </summary>
    /// <remarks>
    /// This is the issuer ('iss' claim) that will be validated in JWT tokens.
    /// Algorithm: RS256 (RSA with SHA-256) for signature verification.
    /// </remarks>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// The audience identifier for this API.
    /// Must match the 'aud' claim in JWT tokens.
    /// </summary>
    /// <remarks>
    /// OWASP A01 (Broken Access Control): Audience validation ensures
    /// tokens are intended for this specific API.
    /// </remarks>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 client ID for this application.
    /// For local development: test-client
    /// For production: configured in AWS Cognito app client
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth2 client secret for this application.
    /// WARNING: Only used for local development with LocalStack.
    /// Production uses AWS Cognito managed secrets.
    /// </summary>
    /// <remarks>
    /// Security: This value should be stored in AWS Secrets Manager in production.
    /// LocalStack development: test-secret (explicitly documented as test-only)
    /// </remarks>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Token validation requires HTTPS for the metadata endpoint.
    /// Set to false only for LocalStack development.
    /// </summary>
    /// <remarks>
    /// OWASP A02 (Cryptographic Failures): HTTPS enforcement for production.
    /// LocalStack limitation: HTTP only in local environment.
    /// </remarks>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Validate the token issuer ('iss' claim) against the configured Authority.
    /// </summary>
    /// <remarks>
    /// JWT Security: Always true to prevent token substitution attacks.
    /// Reference: technical-decisions.md JWT-Specific Vulnerabilities
    /// </remarks>
    public bool ValidateIssuer { get; set; } = true;

    /// <summary>
    /// Validate the token audience ('aud' claim) against the configured Audience.
    /// </summary>
    /// <remarks>
    /// JWT Security: Always true to prevent token reuse across APIs.
    /// OWASP A01: Broken Access Control prevention.
    /// </remarks>
    public bool ValidateAudience { get; set; } = true;

    /// <summary>
    /// Validate the token lifetime (exp, nbf claims).
    /// </summary>
    /// <remarks>
    /// JWT Security: Always true to prevent expired/premature token use.
    /// OWASP A07: Identification and Authentication Failures prevention.
    /// </remarks>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>
    /// Validate the token signature using the issuer's public key (JWKS).
    /// </summary>
    /// <remarks>
    /// JWT Security: Always true to prevent token tampering.
    /// Algorithm: RS256 only (reject alg: none or HS256 in public scenarios).
    /// CVE-2015-9235: Algorithm confusion attack prevention.
    /// </remarks>
    public bool ValidateIssuerSigningKey { get; set; } = true;

    /// <summary>
    /// Clock skew tolerance for token expiration checks.
    /// Default: 5 minutes to account for server time differences.
    /// </summary>
    /// <remarks>
    /// JWT Security: Reasonable tolerance to prevent false expiration rejections.
    /// Reference: technical-decisions.md Token Validation Checklist
    /// </remarks>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Validates that all required configuration values are present and valid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    /// <remarks>
    /// Fail-fast pattern: Validate configuration at application startup.
    /// Reference: technical-decisions.md Configuration-First Principle
    /// </remarks>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Authority))
        {
            throw new InvalidOperationException(
                "Authentication configuration error: Authority is required. " +
                "Configure AWS:Cognito:Authority in appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException(
                "Authentication configuration error: Audience is required. " +
                "Configure AWS:Cognito:Audience in appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(ClientId))
        {
            throw new InvalidOperationException(
                "Authentication configuration error: ClientId is required. " +
                "Configure AWS:Cognito:ClientId in appsettings.json.");
        }

        // ClientSecret is optional (only required for confidential clients)
        // LocalStack development uses test-secret, production may not need it
    }
}
